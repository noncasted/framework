using UnityEngine;

namespace Global.Telemetry
{
    // What the game set that changes the cost of a frame: settings.quality (the index of the level), settings.vsync,
    // settings.target_fps (-1: the default of the platform), settings.time_scale, settings.msaa (samples; a render pipeline may set it).
    public class SettingsTelemetryCollector : ITelemetryCollector
    {
        public void Collect(ITelemetrySample sample)
        {
            sample.Gauge("settings.quality", QualitySettings.GetQualityLevel());
            sample.Gauge("settings.vsync", QualitySettings.vSyncCount, TelemetryUnits.Count);
            sample.Gauge("settings.target_fps", Application.targetFrameRate, TelemetryUnits.Fps);
            sample.Gauge("settings.time_scale", Time.timeScale);
            sample.Gauge("settings.msaa", QualitySettings.antiAliasing, TelemetryUnits.Count);
        }
    }
}
