using System;
using System.Collections.Generic;
using Internal;

namespace Global.Telemetry
{
    // The collectors of IClientTelemetry.AddCollector: of a scope below the one of the telemetry (a run under the meta). Itself
    // one collector of the module, so the sampler and the sessions see none of them apart. A collector stays while its lifetime
    // lives, over any number of sessions; one that is also live is started for each session it is in, with a lifetime that ends
    // with the session or with its own lifetime, so what it keeps of a session does not reach the next one. One that throws is
    // logged and left in, as the collectors of the scope.
    public class TelemetryAddedCollectors : ITelemetryCollector, ITelemetryLiveCollector
    {
        private readonly List<(ITelemetryCollector Collector, IReadOnlyLifetime Lifetime)> _added = new();
        private ITelemetryCollector[] _snapshot = Array.Empty<ITelemetryCollector>();
        private IReadOnlyLifetime _running;

        public void Add(IReadOnlyLifetime lifetime, ITelemetryCollector collector)
        {
            if (collector == null)
                throw new ArgumentNullException(nameof(collector));

            if (lifetime.IsTerminated)
                return;

            _added.Add((collector, lifetime));
            TakeSnapshot();
            lifetime.Listen(() => Remove(collector, lifetime));

            if (_running != null && _running.IsTerminated == false)
                StartOne(collector, lifetime);
        }

        public void Start(IReadOnlyLifetime running)
        {
            _running = running;

            foreach (var (collector, lifetime) in _added.ToArray())
                StartOne(collector, lifetime);
        }

        public void Collect(ITelemetrySample sample)
        {
            var collectors = _snapshot;

            for (var i = 0; i < collectors.Length; i++)
            {
                // The isolation of subscribers, as in TelemetrySampler.
                try
                {
                    collectors[i].Collect(sample);
                }
                catch (Exception exception)
                {
                    TelemetrySelfLog.Exception(exception);
                }
            }
        }

        private void StartOne(ITelemetryCollector collector, IReadOnlyLifetime lifetime)
        {
            if (collector is not ITelemetryLiveCollector live)
                return;

            try
            {
                live.Start(_running.Intersect(lifetime));
            }
            catch (Exception exception)
            {
                TelemetrySelfLog.Exception(exception);
            }
        }

        private void Remove(ITelemetryCollector collector, IReadOnlyLifetime lifetime)
        {
            _added.Remove((collector, lifetime));
            TakeSnapshot();
        }

        private void TakeSnapshot()
        {
            _snapshot = new ITelemetryCollector[_added.Count];

            for (var i = 0; i < _added.Count; i++)
                _snapshot[i] = _added[i].Collector;
        }
    }
}
