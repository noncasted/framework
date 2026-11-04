using Global.Telemetry;

namespace Global.Resolution
{
    // The resolution setting of the player in every sample: settings.resolution, the value of ResolutionMode (0 auto, 1 low,
    // 2 high). What it gives is render.width / render.height / render.dpr of the standard collectors.
    public class ResolutionTelemetryCollector : ITelemetryCollector
    {
        public ResolutionTelemetryCollector(IResolutionSetting setting)
        {
            _setting = setting;
        }

        private readonly IResolutionSetting _setting;

        public void Collect(ITelemetrySample sample)
        {
            sample.Gauge("settings.resolution", (int)_setting.Resolution.Value);
        }
    }
}
