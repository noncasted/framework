using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Infrastructure.Telemetry;

// The instrumentation every tracer and meter provider of the backend has, a host's or the one of a monolith process.
// The sources and the meter of the framework features are not listed: each feature registers its own
// (AddTraceSource, AddBackendMeter).
public static class TelemetryInstrumentation
{
    public const string HealthEndpointPath = "/health";
    public const string AlivenessEndpointPath = "/alive";

    public static TracerProviderBuilder AddDefaultInstrumentation(this TracerProviderBuilder tracing)
    {
        return tracing.AddAspNetCoreInstrumentation(options => options.Filter = context =>
                          context.Request.Path.StartsWithSegments(HealthEndpointPath) == false &&
                          context.Request.Path.StartsWithSegments(AlivenessEndpointPath) == false)
                      .AddHttpClientInstrumentation();
    }

    public static MeterProviderBuilder AddDefaultInstrumentation(this MeterProviderBuilder metrics)
    {
        return metrics.AddAspNetCoreInstrumentation()
                      .AddHttpClientInstrumentation()
                      .AddRuntimeInstrumentation()
                      .AddMeter("Microsoft.Orleans");
    }
}
