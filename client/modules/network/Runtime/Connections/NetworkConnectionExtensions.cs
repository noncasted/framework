using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public static class NetworkConnectionExtensions
    {
        public static IScopeBuilder AddNetworkConnection(this IScopeBuilder builder)
        {
            builder.Register<NetworkConnection>()
                   .As<INetworkConnection>()
                   .AsSelf();

            builder.Register<WebSocketFactory>()
                   .As<IWebSocketFactory>();

            builder.Register<NetworkCommandsCollection>()
                   .AsSelfResolvable()
                   .As<INetworkCommandsCollection>();

            return builder;
        }

        public static void OneWay(this INetworkConnection connection, INetworkContext value)
        {
            connection.Writer.WriteOneWay(value);
        }

        // A request of the session layer; failures as in IConnectionWriter.WriteRequest. A closed connection is a
        // cancelled operation: whatever was being done over the session (entering it, creating an entity) stops
        // there, and the loss itself is handled by the listener of Closed.
        public static UniTask<T> Request<T>(this INetworkConnection connection, INetworkContext commandContext)
        {
            return connection.Writer.WriteRequest<T>(commandContext);
        }

        public static UniTask<EmptyResponse> Request(this INetworkConnection connection, INetworkContext commandContext)
        {
            return connection.Request<EmptyResponse>(commandContext);
        }

        public static UniTask ForceSendAll(this INetworkConnection connection)
        {
            return connection.Writer.ForceSendAll();
        }
    }
}
