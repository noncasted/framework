using System.Collections.Generic;
using Internal;

namespace Global.Telemetry
{
    // The loads of assets in a session (AssetLoads of the core: the catalogue groups, the scenes of the scopes) as marks with the
    // key of the code in the detail: load.start, load.end and load.failed with the duration, load.slow once when a load runs over
    // SlowLoadMs, and load.unfinished with the time so far for what is still loading when the session ends. A load that began
    // before the session is followed from its start, without a start mark.
    public class AssetLoadTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector
    {
        public AssetLoadTelemetryCollector(ClientTelemetryOptions options, TelemetryBuffer buffer, ITelemetryClock clock)
        {
            _options = options;
            _buffer = buffer;
            _clock = clock;
        }

        private readonly ClientTelemetryOptions _options;
        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryClock _clock;
        private readonly List<AssetLoad> _running = new();
        private readonly HashSet<AssetLoad> _slow = new();

        public void Start(IReadOnlyLifetime running)
        {
            _running.Clear();
            _slow.Clear();
            _running.AddRange(AssetLoads.Running);

            AssetLoads.Started.Advise(running, OnStarted);
            AssetLoads.Ended.Advise(running, OnEnded);
            running.Listen(OnSessionEnd);
        }

        public void Collect(ITelemetrySample sample)
        {
            foreach (var load in _running)
            {
                if (load.ElapsedMs < _options.SlowLoadMs || _slow.Add(load) == false)
                    continue;

                Mark("load.slow", load.Key, load.ElapsedMs);
            }
        }

        private void OnStarted(AssetLoad load)
        {
            _running.Add(load);
            Mark("load.start", load.Key, null);
        }

        private void OnEnded(AssetLoad load)
        {
            _running.Remove(load);
            _slow.Remove(load);

            if (load.Error == null)
                Mark("load.end", load.Key, load.ElapsedMs);
            else
                Mark("load.failed", $"{load.Key} ({load.Error.GetType().Name})", load.ElapsedMs);
        }

        private void OnSessionEnd()
        {
            foreach (var load in _running)
                Mark("load.unfinished", load.Key, load.ElapsedMs);

            _running.Clear();
            _slow.Clear();
        }

        private void Mark(string name, string detail, double? durationMs)
        {
            _buffer.AddMark(_clock.ElapsedMilliseconds, name, detail, durationMs == null ? (long?)null : (long)durationMs.Value);
        }
    }
}
