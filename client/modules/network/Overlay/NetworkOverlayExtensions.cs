namespace Internal
{
    public static class NetworkOverlayExtensions
    {
        // The debug overlay of the latency monitor (N, a three finger tap): in the scope of a session, next to
        // AddSessionServices, which registers the monitor. The screen canvases of the root scope (IScreenCanvasCamera) say
        // where it is drawn.
        public static IScopeBuilder AddNetworkLatencyOverlay(this IScopeBuilder builder, NetworkOverlayOptions options)
        {
            builder.RegisterInstance(options);

            builder.Register<NetworkLatencyOverlaySetup>()
                   .As<IScopeSetup>();

            return builder;
        }
    }
}
