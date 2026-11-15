using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Global.MatchMetrics
{
    // The streams of the scope and their ticks in real time: every Tick the clocks are checked, the batches due are cut and
    // the senders try again. A stream ended goes on sending until its sender is idle or the scope is unloaded.
    public class MatchMetrics : IMatchMetrics, IScopeSetup
    {
        public MatchMetrics(MatchMetricsOptions options, IMatchMetricsTransport transport, IMatchMetricsTicks ticks)
        {
            _options = options;
            _transport = transport;
            _ticks = ticks;
        }

        private readonly MatchMetricsOptions _options;
        private readonly IMatchMetricsTransport _transport;
        private readonly IMatchMetricsTicks _ticks;
        private readonly List<MatchMetricsEntry> _entries = new();

        public int Count => _entries.Count;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            Run(lifetime).NoAwait();
        }

        public IMatchMetricsStream Open(IReadOnlyLifetime lifetime, Guid matchId, IMatchMetricsClock clock)
        {
            var open = new SharedMatchMetrics.Open
            {
                MatchId = matchId,
                WriterId = Guid.NewGuid(),
                Kind = SharedMatchMetrics.MatchStreamKind.Client
            };

            var stream = new MatchMetricsStream(clock, _ticks);
            var sender = new MatchMetricsSender(_transport, _ticks, new MatchMetricsBuffer(_options.BufferedBatches), open, _options.SendIntervalMs);
            var entry = new MatchMetricsEntry(stream, sender, _ticks.NowMs);

            _entries.Add(entry);
            lifetime.Listen(entry.End);

            return stream;
        }

        public void Tick()
        {
            var nowMs = _ticks.NowMs;

            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                entry.Tick(nowMs, _options.SendIntervalMs);

                if (entry.IsDone)
                    _entries.RemoveAt(i);
            }
        }

        // The top of a task nobody awaits: the end of the scope is the way out; a bug is logged and the ticks stop.
        private async UniTask Run(IReadOnlyLifetime lifetime)
        {
            try
            {
                while (true)
                {
                    await UniTask.Delay(_options.Tick, DelayType.Realtime, PlayerLoopTiming.Update, lifetime.Token);
                    Tick();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
