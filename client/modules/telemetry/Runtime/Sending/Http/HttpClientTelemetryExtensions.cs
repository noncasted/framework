using Internal;

namespace Global.Telemetry
{
    public static class HttpClientTelemetryExtensions
    {
        // AddClientTelemetry with the transport over HTTP: JSON POSTs to <http.BaseUrl>/start, /batch, /end, /startup (on the web a
        // path of the server of the page, which takes them to MapClientTelemetryHttpIntake of the backend). Needs no socket,
        // no network module and no MemoryPack.
        public static IScopeBuilder AddHttpClientTelemetry(this IScopeBuilder builder, ClientTelemetryOptions options, TelemetryHttpOptions http)
        {
            builder.RegisterInstance(http);

#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<WebTelemetryHttp>()
                   .As<ITelemetryHttp>();
#else
            builder.Register<NetTelemetryHttp>()
                   .As<ITelemetryHttp>();
#endif

            builder.Register<TelemetryHttpTransport>()
                   .As<ITelemetryTransport>();

            return builder.AddClientTelemetry(options);
        }
    }
}
