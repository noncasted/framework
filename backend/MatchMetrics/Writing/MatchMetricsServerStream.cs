using Common.Extensions;
using Internal;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.MatchMetrics;

// The stream of the server: the values wait in memory and go as batches of the same storage path as a client's. A periodic
// write takes the seconds already over only (a gauge may still change in the current one); the last write at the end of
// the lifetime takes everything. A failed write keeps the values for the next one: the loop is the top of a task nobody
// awaits, so it logs the failure.
public sealed class MatchMetricsServerStream : IMatchMetricsServerStream
{
    public MatchMetricsServerStream(
        Guid matchId,
        MatchMetricStreamStore streams,
        MatchMetricBatchWriter writer,
        TimeProvider time,
        ILogger<MatchMetricsServerStream> logger)
    {
        _matchId = matchId;
        _streams = streams;
        _writer = writer;
        _time = time;
        _logger = logger;
        _openedAt = Now;
    }

    private readonly Guid _matchId;
    private readonly Guid _writerId = Guid.NewGuid();
    private readonly MatchMetricStreamStore _streams;
    private readonly MatchMetricBatchWriter _writer;
    private readonly TimeProvider _time;
    private readonly ILogger<MatchMetricsServerStream> _logger;
    private readonly DateTime _openedAt;
    private readonly SemaphoreSlim _writes = new(1, 1);

    private readonly object _lock = new();
    private readonly Dictionary<string, int> _slots = new();
    private readonly Dictionary<int, SortedDictionary<int, float>> _gauges = new();
    private readonly List<(string Name, float Time, string Fields)> _events = new();

    private MatchMetricStream? _stream;

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
            _events.Add((name, (float)Seconds, json));
    }

    public void Gauge(string name, float value)
    {
        RequireName(name);

        if (float.IsFinite(value) == false)
            throw new ArgumentException($"[MatchMetrics] The gauge {name} is not a finite number: {value}");

        lock (_lock)
        {
            if (_slots.TryGetValue(name, out var slot) == false)
            {
                if (_slots.Count >= SharedMatchMetricsLimits.MaxSeries)
                    throw new ArgumentException($"[MatchMetrics] Over {SharedMatchMetricsLimits.MaxSeries} series in the stream of the server");

                slot = _slots.Count;
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
        }

        await Flush(true);
    }

    private async Task Flush(bool isLast)
    {
        await _writes.WaitAsync();

        try
        {
            _stream ??= await _streams.Open(_matchId, Guid.Empty, _writerId, SharedMatchMetrics.MatchStreamKind.Server, _openedAt);

            var now = Now;
            var until = isLast ? int.MaxValue : (int)Seconds;
            var batch = Take(_stream, until, now, out var events);

            if (batch.Chunks.Count == 0 && batch.Events.Count == 0)
                return;

            var result = await _writer.Write(_stream, batch, now);

            if (result != MatchMetricWrite.Stored)
                throw new InvalidOperationException($"[MatchMetrics] The batch {batch.Seq} of the server is {result}: the server is its only writer");

            Remove(until, events);
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

    private static void RequireName(string name)
    {
        if (SharedMatchMetricNames.IsName(name) == false)
            throw new ArgumentException($"[MatchMetrics] The name '{name}': only [a-z0-9._] up to {SharedMatchMetricsLimits.MaxNameLength}");
    }
}
