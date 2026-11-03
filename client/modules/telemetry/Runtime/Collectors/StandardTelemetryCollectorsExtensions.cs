using Internal;

namespace Global.Telemetry
{
    public static class StandardTelemetryCollectorsExtensions
    {
        // The base metrics of any game, next to AddClientTelemetry. The scope needs IUpdater (AddUpdater). What they cost per frame:
        // FramePhaseCollectorsExtensions.
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

            builder.AddFramePhaseCollectors();

            builder.Register<SettingsTelemetryCollector>()
                   .As<ITelemetryCollector>();

#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<TelemetryWebRenderSize>()
                   .As<ITelemetryRenderSize>();
#else
            builder.Register<UnityTelemetryRenderSize>()
                   .As<ITelemetryRenderSize>();
#endif

            builder.Register<RenderSizeTelemetryCollector>()
                   .As<ITelemetryCollector>();

#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<TelemetryWebMemory>()
                   .As<ITelemetryWebMemory>();

            builder.Register<WebMemoryTelemetryCollector>()
                   .As<ITelemetryCollector>();

            builder.Register<TelemetryRaf>()
                   .As<ITelemetryRaf>();

            builder.Register<RafTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>();

            builder.Register<TelemetryWebGlInfo>()
                   .As<ITelemetryWebGlInfo>();

            builder.Register<WebGlInfoTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>();
#endif

            return builder;
        }
    }
}
