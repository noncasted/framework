using Cysharp.Threading.Tasks;

namespace Internal
{
    // The silence rule (ConnectionSilence) on the connection of a session of the game: the server answers the pings of
    // NetworkLatencyMonitor once it knows this user.
    public class NetworkConnectionWatchdog : IScopeSetup
    {
        public NetworkConnectionWatchdog(INetworkConnection connection, INetworkSession session)
        {
            _connection = connection;
            _session = session;
        }

        private readonly INetworkConnection _connection;
        private readonly INetworkSession _session;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            Run(lifetime).NoAwait();
        }

        private async UniTask Run(IReadOnlyLifetime lifetime)
        {
            // The pings go out once the backend knows this user.
            await UniTask.WaitUntil(() => _session.Users.Local != null, cancellationToken: lifetime.Token);
            await ConnectionSilence.Watch(lifetime, _connection);
        }
    }
}
