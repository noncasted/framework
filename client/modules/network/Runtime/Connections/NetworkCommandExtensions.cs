namespace Internal
{
    public static class NetworkCommandExtensions
    {
        public static ServiceRegistration RegisterCommand<T>(this IScopeBuilder builder) where T : INetworkCommand
        {
            var registration = builder.Register<CommandResolver<T>>();
            registration.AsSelfResolvable();

            return builder.Register<T>();
        }

        public class CommandResolver<T> where T : INetworkCommand
        {
            public CommandResolver(T command, INetworkCommandsCollection collection)
            {
                collection.Add(command);
            }
        }
    }
}
