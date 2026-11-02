namespace Internal
{
    public static class SessionServicesExtensions
    {
        public static IScopeBuilder AddSessionServices(this IScopeBuilder builder)
        {
            AddEntityServices();
            AddEntityCommands();

            AddConnectionServices();

            AddUserServices();
            AddUserCommands();

            AddLatencyServices();

            return builder;

            void AddEntityServices()
            {
                builder.Register<NetworkObjectsCollection>()
                       .As<INetworkObjectsCollection>();

                builder.Register<NetworkEntityCollection>()
                       .As<INetworkEntitiesCollection>();

                builder.Register<NetworkEntitiesIds>()
                       .As<INetworkEntityIds>();

                builder.Register<NetworkEntityFactory>()
                       .As<INetworkEntityFactory>();

                builder.Register<NetworkPropertiesCollector>()
                       .As<INetworkDirtyProperties>()
                       .As<IScopeSetup>();

                builder.Register<NetworkEntityDestroyer>()
                       .As<INetworkEntityDestroyer>();
            }

            void AddEntityCommands()
            {
                builder.RegisterCommand<EntityCreatedCommand>();
                builder.RegisterCommand<EntityDestroyedCommand>();
                builder.RegisterCommand<EntityPropertyUpdateCommand>();
                builder.RegisterCommand<PlayerDisconnectedCommand>();
                builder.RegisterCommand<EntityEventCommand>();
            }

            void AddConnectionServices()
            {
                builder.AddNetworkConnection();

                builder.Register<SessionConnection>()
                       .As<ISessionConnection>();

                builder.Register<NetworkSession>()
                       .As<INetworkSession>();

                builder.Register<NetworkSessionCallbacks>()
                       .As<INetworkSessionCallbacks>();
            }

            void AddUserServices()
            {
                builder.Register<NetworkUsersCollection>()
                       .As<INetworkUsersCollection>();
            }

            void AddUserCommands()
            {
                builder.RegisterCommand<LocalUserUpdateCommand>();
                builder.RegisterCommand<RemoteUserUpdateCommand>();
                builder.RegisterCommand<RemoteUserAwayCommand>();
            }

            void AddLatencyServices()
            {
                // Set by the entry of a P2P match (its host, the routes of the guests), read by the monitor.
                builder.Register<PeerMatch>()
                       .As<IPeerMatch>();

                builder.Register<LatencyClock>()
                       .As<ILatencyClock>();

                builder.Register<NetworkLatencyMonitor>()
                       .As<INetworkLatencyMonitor>()
                       .As<IScopeSetup>();

                builder.Register<NetworkConnectionWatchdog>()
                       .As<IScopeSetup>();

                builder.RegisterCommand<PongCommand>();
                builder.RegisterCommand<PeerProbeCommand>();
                builder.RegisterCommand<PeerEchoCommand>();
            }

        }
    }
}