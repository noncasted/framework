using Internal;
using UnityEngine;

namespace Global.Telemetry
{
    public static class ClientTelemetryExtensions
    {
        // AddClientTelemetry with the transport over the meta socket. The scope or one above it needs an ITelemetrySocket, so a game
        // calls this in the scope of its meta backend, not above it; the scopes below that one open the sessions.
        public static IScopeBuilder AddSocketClientTelemetry(this IScopeBuilder builder, ClientTelemetryOptions options)
        {
            builder.Register<TelemetrySocketTransport>()
                   .As<ITelemetryTransport>();

            builder.Register<TelemetryRoundTrip>();

            builder.Register<MetaNetworkTelemetryCollector>()
                   .As<ITelemetryCollector>()
                   .As<ITelemetryLiveCollector>();

            return builder.AddClientTelemetry(options);
        }

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
#endif

            builder.Register<AssetLoadTelemetryCollector>()
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
