using Common.Extensions;
using Internal;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.MatchMetrics;

// The stream of the server: the values wait in memory and go as batches of the same storage path as a client's. A periodic
// write takes the seconds already over only (a gauge may still change in the current one); the last write at the end of
// the lifetime takes everything, a write after it throws. A failed write keeps its batch and sends the same one again first
// (a commit that threw may have gone through: then it is a Duplicate). A series the dictionary refuses (the name is a
// counter of the game) is dropped with its values, and a later write of it throws. The loop is the top of a task nobody
// awaits, so it logs the failures.
internal sealed class MatchMetricsServerStream : IMatchMetricsServerStream
{
    public MatchMetricsServerStream(
        Guid matchId,
        MatchMetricStreamStore streams,
        MatchMetricBatchWriter writer,
        MatchMetricSeriesDictionary dictionary,
        TimeProvider time,
        ILogger<MatchMetricsServerStream> logger)
    {
        _matchId = matchId;
        _streams = streams;
        _writer = writer;
        _dictionary = dictionary;
        _time = time;
        _logger = logger;
        _openedAt = Now;
    }

    private readonly Guid _matchId;
    private readonly Guid _writerId = Guid.NewGuid();
    private readonly MatchMetricStreamStore _streams;
    private readonly MatchMetricBatchWriter _writer;
    private readonly MatchMetricSeriesDictionary _dictionary;
    private readonly TimeProvider _time;
    private readonly ILogger<MatchMetricsServerStream> _logger;
    private readonly DateTime _openedAt;
    private readonly SemaphoreSlim _writes = new(1, 1);

    private readonly object _lock = new();
    private readonly Dictionary<string, int> _slots = new();
    private readonly Dictionary<int, SortedDictionary<int, float>> _gauges = new();
    private readonly Dictionary<string, string> _rejected = new();
    private readonly List<(string Name, float Time, string Fields)> _events = new();

    private MatchMetricStream? _stream;
    private Pending? _pending;
    private int _nextSlot;
    private bool _isEnded;

    private DateTime Now => _time.GetUtcNow().UtcDateTime;
    private double Seconds => (Now - _openedAt).TotalSeconds;

    public void Start(IReadOnlyLifetime lifetime, TimeSpan flush)
    {
        Run(lifetime, flush).NoAwait();
    }

    public void Event(string name, params SharedMatchMetrics.MatchEventField[] fields)
    {
        RequireName(name);
        var json = MatchMetricsBatchParser.ParseFields(fields);

        lock (_lock)
        {
            RequireOpen();
            _events.Add((name, (float)Seconds, json));
        }
    }

    public void Gauge(string name, float value)
    {
        RequireName(name);

        if (float.IsFinite(value) == false)
            throw new ArgumentException($"[MatchMetrics] The gauge {name} is not a finite number: {value}");

        lock (_lock)
        {
            RequireOpen();

            if (_rejected.TryGetValue(name, out var reason))
                throw new ArgumentException($"[MatchMetrics] The gauge {name} of the server is rejected: {reason}");

            if (_slots.TryGetValue(name, out var slot) == false)
            {
                if (_nextSlot >= SharedMatchMetricsLimits.MaxSeries)
                    throw new ArgumentException($"[MatchMetrics] Over {SharedMatchMetricsLimits.MaxSeries} series in the stream of the server");

                slot = _nextSlot++;
                _slots.Add(name, slot);
                _gauges.Add(slot, new SortedDictionary<int, float>());
            }

            _gauges[slot][(int)Seconds] = value;
        }
    }

    private async Task Run(IReadOnlyLifetime lifetime, TimeSpan flush)
    {
        using var timer = new PeriodicTimer(flush, _time);

        try
        {
            while (await timer.WaitForNextTickAsync(lifetime.Token))
                await Flush(false);
        }
        catch (OperationCanceledException)
        {
            // The lifetime ended: the last write below takes everything.
        }

        await Flush(true);
    }

    private async Task Flush(bool isLast)
    {
        await _writes.WaitAsync();

        try
        {
            if (isLast)
            {
                lock (_lock)
                    _isEnded = true;
            }

            _stream ??= await _streams.Open(_matchId, Guid.Empty, _writerId, SharedMatchMetrics.MatchStreamKind.Server, _openedAt);

            if (_pending != null)
                await Send(_stream, _pending);

            await ResolveNewSeries(_stream);

            var now = Now;
            var until = isLast ? int.MaxValue : (int)Seconds;
            var batch = Take(_stream, until, now, out var events);

            if (batch.Chunks.Count == 0 && batch.Events.Count == 0)
                return;

            _pending = new Pending(batch, until, events);
            await Send(_stream, _pending);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[MatchMetrics] The write of the stream of the server of the match {Match} failed", _matchId);
        }
        finally
        {
            _writes.Release();
        }
    }

    // Duplicate: an earlier send of this batch was stored, its answer lost.
    private async Task Send(MatchMetricStream stream, Pending pending)
    {
        var result = await _writer.Write(stream, pending.Batch, Now);

        if (result == MatchMetricWrite.Gap)
            throw new InvalidOperationException($"[MatchMetrics] The batch {pending.Batch.Seq} of the server is a Gap: the server is its only writer");

        Remove(pending.Until, pending.Events);
        _pending = null;
    }

    // The kind of a name is the dictionary's: a refusal is the same on every write, so the series is dropped, not sent again.
    private async Task ResolveNewSeries(MatchMetricStream stream)
    {
        List<string> names;

        lock (_lock)
            names = _slots.Where(entry => stream.Slots.ContainsKey(entry.Value) == false).Select(entry => entry.Key).ToList();

        foreach (var name in names)
        {
            try
            {
                await _dictionary.Resolve(new MatchSeriesKey(name, "{}"), SharedMatchMetrics.MatchSeriesKind.Gauge);
            }
            catch (MatchMetricsRejectedException exception)
            {
                _logger.LogError("[MatchMetrics] The gauge {Name} of the server of the match {Match} is dropped: {Reason}", name, _matchId, exception.Message);
                Drop(name, exception.Message);
            }
        }
    }

    private void Drop(string name, string reason)
    {
        lock (_lock)
        {
            _gauges.Remove(_slots[name]);
            _slots.Remove(name);
            _rejected.Add(name, reason);
        }
    }

    // The values of the seconds before until and the events so far.
    private ParsedMatchBatch Take(MatchMetricStream stream, int until, DateTime now, out int events)
    {
        lock (_lock)
        {
            var series = new List<ParsedMatchSeries>();
            var chunks = new List<ParsedMatchChunk>();

            foreach (var (name, slot) in _slots)
            {
                var values = _gauges[slot].Where(value => value.Key < until).ToList();

                if (values.Count == 0)
                    continue;

                if (stream.Slots.ContainsKey(slot) == false)
                    series.Add(new ParsedMatchSeries(slot, new MatchSeriesKey(name, "{}"), SharedMatchMetrics.MatchSeriesKind.Gauge));

                var start = values[0].Key;
                var buckets = Enumerable.Repeat(float.NaN, values[^1].Key - start + 1).ToArray();

                foreach (var (second, value) in values)
                    buckets[second - start] = value;

                chunks.Add(new ParsedMatchChunk(slot, start, AgeOf(start, now), buckets));
            }

            events = _events.Count;

            var parsed = _events.Select(entry => new ParsedMatchEvent(entry.Name, entry.Time, AgeOf(entry.Time, now), entry.Fields)).ToList();

            return new ParsedMatchBatch(stream.LastSeq + 1, series, chunks, parsed);
        }
    }

    private void Remove(int until, int events)
    {
        lock (_lock)
        {
            _events.RemoveRange(0, events);

            foreach (var values in _gauges.Values)
            {
                foreach (var second in values.Keys.Where(second => second < until).ToList())
                    values.Remove(second);
            }
        }
    }

    private long AgeOf(double seconds, DateTime now)
    {
        return Math.Max(0, (long)(now - _openedAt.AddSeconds(seconds)).TotalMilliseconds);
    }

    private void RequireOpen()
    {
        if (_isEnded)
            throw new InvalidOperationException($"[MatchMetrics] The stream of the server of the match {_matchId} is ended");
    }

    private static void RequireName(string name)
    {
        if (SharedMatchMetricNames.IsName(name) == false)
            throw new ArgumentException($"[MatchMetrics] The name '{name}': only [a-z0-9._] up to {SharedMatchMetricsLimits.MaxNameLength}");
    }

    private sealed record Pending(ParsedMatchBatch Batch, int Until, int Events);
}
