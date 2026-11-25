using Internal;
using UnityEngine;

namespace Global.Telemetry
{
    public static class ClientTelemetryExtensions
    {
        // The scope needs an ITelemetryTransport, registered by the game. The base metrics: AddStandardTelemetryCollectors. A
        // game adds its own collectors next to this call: builder.Register<MyCollector>().As<ITelemetryCollector>(), or from a scope
        // below with IClientTelemetry.AddCollector. Nothing runs until the game opens a session (IClientTelemetry.StartSession).
        public static IScopeBuilder AddClientTelemetry(this IScopeBuilder builder, ClientTelemetryOptions options)
        {
            builder.RegisterInstance(options);

            builder.Register<TelemetryClock>()
                   .As<ITelemetryClock>();

            builder.Register<TelemetryTicks>()
                   .As<ITelemetryTicks>();

            builder.Register<TelemetryNameGuard>()
                   .WithParameter(Debug.isDebugBuild);

#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<TelemetryPage>()
                   .As<ITelemetryPage>();
#else
            builder.Register<NoTelemetryPage>()
                   .As<ITelemetryPage>();
#endif

            builder.Register<TelemetryVisibility>();

            builder.Register<PageLifecycleTelemetryCollector>()
                   .As<ITelemetryLiveCollector>();

            builder.Register<PageResizeTelemetryCollector>()
                   .As<ITelemetryLiveCollector>();

#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<TelemetryWebTraceStore>()
                   .As<ITelemetryTraceStore>();
#else
            builder.Register<TelemetryFileTraceStore>()
                   .As<ITelemetryTraceStore>();
#endif

            builder.Register<TelemetryWallClock>()
                   .As<ITelemetryWallClock>();

            builder.Register<TelemetrySessionTrace>();
            builder.Register<TelemetryNameTable>();
            builder.Register<TelemetryBuffer>();
            builder.Register<TelemetrySampler>();

#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<TelemetryWebDeviceSource>()
                   .As<ITelemetryDeviceSource>();
#else
            builder.Register<UnityTelemetryDeviceSource>()
                   .As<ITelemetryDeviceSource>();
#endif

            builder.Register<TelemetryBatchWriter>();

            builder.Register<TelemetryErrors>()
                   .As<ITelemetryErrors>();

            builder.AddTelemetryErrorCatching();

#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<TelemetryGlContext>()
                   .As<ITelemetryGlContext>();

            builder.Register<GlContextTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>();

            builder.Register<TelemetryGlErrors>()
                   .As<ITelemetryGlErrors>();

            builder.Register<GlErrorsTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>();

            builder.Register<TelemetryGlCallErrors>()
                   .As<ITelemetryGlCallErrors>();

            builder.Register<GlCallErrorsTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>();
#endif

            builder.Register<AssetLoadTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>();

            builder.Register<LogMarkTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>();

            builder.Register<TelemetryAddedCollectors>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>()
                   .AsSelf();

            builder.Register<TelemetryCollection>();
            builder.Register<TelemetryRequestGate>();

            builder.Register<ProfilerTelemetryStartupSource>()
                   .As<ITelemetryStartupSource>();

#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<WebTelemetryPageTimeline>()
                   .As<ITelemetryPageTimeline>();
#else
            builder.Register<NoTelemetryPageTimeline>()
                   .As<ITelemetryPageTimeline>();
#endif

            builder.Register<TelemetryStartupPage>();

            builder.Register<TelemetryStartupReport>()
                   .As<IScopeSetup>()
                   .AsSelf();

            builder.Register<ClientTelemetry>()
                   .As<IClientTelemetry>();

            return builder;
        }
    }
}
