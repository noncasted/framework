using Global.Telemetry;
using Internal;

namespace Global.Resolution
{
    // Compiled only with the telemetry module in the project (Internal.Resolution.Telemetry: a version define of its package).
    public static class ResolutionTelemetryExtensions
    {
        // In the scope of the telemetry (after AddSocketClientTelemetry), under a scope with the IResolutionSetting:
        // settings.resolution in every sample (ResolutionTelemetryCollector).
        public static IScopeBuilder AddResolutionTelemetry(this IScopeBuilder builder)
        {
            builder.Register<ResolutionTelemetryCollector>()
                   .As<ITelemetryCollector>();

            return builder;
        }
    }
}
