using Internal;

namespace Global.FrameRate
{
    // Compiled only with the telemetry module in the project (Internal.FrameRate.Telemetry: a version define of its package).
    public static class FrameRateTelemetryExtensions
    {
        // In the scope of the telemetry (after AddHttpClientTelemetry or AddSocketClientTelemetry), under the scope of AddFrameRate: the marks
        // fps.auto.<limit> of the automatic frame rate (FrameRateTelemetryMarks).
        public static IScopeBuilder AddFrameRateTelemetry(this IScopeBuilder builder)
        {
            builder.Register<FrameRateTelemetryMarks>()
                   .As<IScopeSetup>();

            return builder;
        }
    }
}
