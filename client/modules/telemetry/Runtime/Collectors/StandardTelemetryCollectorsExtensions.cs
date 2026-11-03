using Internal;

namespace Global.Telemetry
{
    public static class StandardTelemetryCollectorsExtensions
    {
        // The base metrics of any game, next to AddClientTelemetry. The scope needs IUpdater (AddUpdater).
        public static IScopeBuilder AddStandardTelemetryCollectors(this IScopeBuilder builder)
        {
            builder.Register<FramesTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>();

            builder.Register<UnityTelemetryFrameTimings>()
                   .As<ITelemetryFrameTimings>();

            builder.Register<FrameTimingsTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>();

            builder.Register<UnityTelemetryMemoryReadings>()
                   .As<ITelemetryMemoryReadings>();

            builder.Register<MemoryTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>();

            builder.Register<ProfilerTelemetryStatSource>()
                   .As<ITelemetryStatSource>();

            builder.Register<RenderTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>();

#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<TelemetryWebMemory>()
                   .As<ITelemetryWebMemory>();

            builder.Register<WebMemoryTelemetryCollector>()
                   .As<ITelemetryCollector>();
#endif

            return builder;
        }
    }
}
