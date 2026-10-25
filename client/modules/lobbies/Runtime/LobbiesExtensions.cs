using Internal;

namespace Global.Lobbies
{
    public static class LobbiesExtensions
    {
        // The requests of the local user to the meta: the scope registers its IMetaSocket (the meta backend).
        public static IScopeBuilder AddMatchmaking(this IScopeBuilder builder)
        {
            builder.Register<Matchmaking>()
                   .As<IMatchmaking>();

            return builder;
        }

        // The scope of a lobby session: the lobby service, fetched from the session before the players (the session
        // callbacks run in the order of the setup).
        public static IScopeBuilder AddLobbyNetworkService(this IScopeBuilder builder)
        {
            return builder.AddLobbyNetworkService<LobbyNetworkService>();
        }

        // A lobby service of the game (its own properties, CollectProperties).
        public static IScopeBuilder AddLobbyNetworkService<TService>(this IScopeBuilder builder)
            where TService : LobbyNetworkService
        {
            builder.Register<TService>()
                   .AsSelf()
                   .As<IScopeSetup>();

            return builder;
        }
    }
}
