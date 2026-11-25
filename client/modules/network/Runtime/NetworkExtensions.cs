namespace Internal
{
    public static class NetworkExtensions
    {
        // The root scope of a project with the network: the options, the protocol hash, the offline servers and the sockets
        // of the peers. The
        // connections are added by the scopes that use them (AddNetworkConnection, AddSessionServices).
        public static IScopeBuilder AddNetwork(this IScopeBuilder builder, NetworkOptions options, string protocolHash)
        {
            builder.RegisterInstance(options);
            builder.RegisterInstance(new NetworkProtocolHash(protocolHash));

            builder.Register<LocalServers>()
                   .As<ILocalServers>();

            builder.Register<PeerSockets>()
                   .As<IPeerSockets>();

            return builder;
        }
    }
}
