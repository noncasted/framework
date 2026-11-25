using Internal;

namespace Global.Telemetry
{
    public static class TelemetryErrorCatchingExtensions
    {
        // Live collectors: everything starts with a session and is gone when it ends: no error is taken outside one.
        public static IScopeBuilder AddTelemetryErrorCatching(this IScopeBuilder builder)
        {
            builder.Register<TelemetryErrorQueue>();
            builder.Register<TelemetryTextCleaner>();
            builder.Register<TelemetryErrorGroups>();

            builder.Register<TelemetryLogErrors>()
                   .As<ITelemetryLiveCollector>();

            builder.Register<TelemetryLoggerErrors>()
                   .As<ITelemetryLiveCollector>();

#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<TelemetryBrowserErrors>()
                   .As<ITelemetryLiveCollector>();
#endif

            builder.Register<TelemetryErrorPump>()
                   .As<ITelemetryLiveCollector>();

            return builder;
        }
    }
}
