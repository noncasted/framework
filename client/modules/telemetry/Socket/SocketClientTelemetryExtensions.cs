using Internal;

namespace Global.Telemetry
{
    // Internal.Telemetry.Socket: compiled only with the network module in the project (FRAMEWORK_NETWORK, a version define of its package).
    public static class SocketClientTelemetryExtensions
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
    }
}
