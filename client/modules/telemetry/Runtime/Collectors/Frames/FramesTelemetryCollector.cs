using Internal;
using UnityEngine;

namespace Global.Telemetry
{
    // The real time of the frames, whatever the speed of the updater: the delta it passes is the time of the game.
    // A second without frames (a hidden tab) gives no metrics.
    public class FramesTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector, IUpdatable
    {
        public FramesTelemetryCollector(IUpdater updater)
        {
            _updater = updater;
        }

        private readonly IUpdater _updater;
        private readonly FrameTimeWindow _window = new();

        public void Start(IReadOnlyLifetime running)
        {
            _window.Clear();
            _updater.Add(running, this);
        }

        public void OnUpdate(float delta)
        {
            _window.Add(Time.unscaledDeltaTime);
        }

        public void Collect(ITelemetrySample sample)
        {
            if (_window.TryTake(out var stats) == false)
                return;

            sample.Gauge("frame.fps", stats.Fps, TelemetryUnits.Fps);
            sample.Gauge("frame.dt.avg", stats.AverageMs, TelemetryUnits.Milliseconds);
            sample.Gauge("frame.dt.p50", stats.P50Ms, TelemetryUnits.Milliseconds);
            sample.Gauge("frame.dt.p95", stats.P95Ms, TelemetryUnits.Milliseconds);
            sample.Gauge("frame.dt.max", stats.MaxMs, TelemetryUnits.Milliseconds);
            sample.Gauge("frame.over33", stats.Over33, TelemetryUnits.Count);
            sample.Gauge("frame.over50", stats.Over50, TelemetryUnits.Count);
        }
    }
}
