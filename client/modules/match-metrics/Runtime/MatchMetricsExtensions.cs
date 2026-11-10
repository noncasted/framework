using Internal;

namespace Global.MatchMetrics
{
    public static class MatchMetricsExtensions
    {
        // IMatchMetrics over the meta socket: the scope or one above it needs an IMatchMetricsSocket, so a game calls this in the
        // scope of its meta backend; the scopes of the runs open the streams.
        public static IScopeBuilder AddSocketMatchMetrics(this IScopeBuilder builder, MatchMetricsOptions options = null)
        {
            builder.Register<MatchMetricsSocketTransport>()
                   .As<IMatchMetricsTransport>();

            return builder.AddMatchMetrics(options);
        }

        // The scope needs an IMatchMetricsTransport.
        public static IScopeBuilder AddMatchMetrics(this IScopeBuilder builder, MatchMetricsOptions options = null)
        {
            builder.RegisterInstance(options ?? new MatchMetricsOptions());

            builder.Register<MatchMetricsTicks>()
                   .As<IMatchMetricsTicks>();

            builder.Register<MatchMetrics>()
                   .As<IMatchMetrics>()
                   .As<IScopeSetup>();

            return builder;
        }
    }
}
