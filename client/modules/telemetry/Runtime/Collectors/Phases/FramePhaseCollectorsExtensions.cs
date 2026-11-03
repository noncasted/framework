using Internal;

namespace Global.Telemetry
{
    public static class FramePhaseCollectorsExtensions
    {
        // The collectors of every frame: the phases, the spikes, the allocations and the timer of the GPU, all on the markers of
        // one FramePhaseLoop. Part of AddStandardTelemetryCollectors, which registers ITelemetryMemoryReadings.
        public static IScopeBuilder AddFramePhaseCollectors(this IScopeBuilder builder)
        {
            builder.Register<FramePhaseClock>();

            builder.Register<FramePhaseLoop>()
                   .As<ITelemetryLiveCollector>();

            builder.Register<FramePhasesTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>()
                   .As<IFramePhaseListener>();

            builder.Register<FrameSpikesTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>()
                   .As<IFramePhaseListener>();

            builder.Register<GcAllocationsTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>()
                   .As<IFramePhaseListener>();

#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<TelemetryGpuQuery>()
                   .As<ITelemetryGpuQuery>();

            builder.Register<GpuQueryTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>()
                   .As<IFramePhaseListener>();
#endif

            return builder;
        }
    }
}
