using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// a connection lost without a close (a dropped network) is noticed by the socket only after minutes. In a
    /// session the server answers the ping of NetworkLatencyMonitor every ProbeInterval (0.5 s), so a healthy
    /// connection, idle or not, never stays silent for long: a server silent for SilenceLimit is a loss, the connection
    /// is dropped (INetworkConnection.Drop) and Closed goes to the code that handles losses (the reconnect of a match).
    /// The silence is counted in frame time capped per frame: a stalled main thread (a paused editor, a long load)
    /// is not a silence of the server.
    /// </summary>
    public class NetworkConnectionWatchdog : IScopeSetup
    {
        public NetworkConnectionWatchdog(INetworkConnection connection, INetworkSession session)
        {
            _connection = connection;
            _session = session;
        }

        public const float SilenceLimit = 10f;

        private const float MaxFrameDelta = 0.25f;

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

            var received = _connection.Traffic.ReceivedMessages;
            var silence = 0f;

            while (lifetime.IsTerminated == false)
            {
                await UniTask.Yield(lifetime.Token);

                var current = _connection.Traffic.ReceivedMessages;

                if (current != received)
                {
                    received = current;
                    silence = 0f;
                    continue;
                }

                silence += Mathf.Min(Time.unscaledDeltaTime, MaxFrameDelta);

                if (silence < SilenceLimit)
                    continue;

                Debug.LogWarning($"[Network] [Watchdog] No message from the server for {SilenceLimit} s: the connection is lost");
                _connection.Drop($"no message from the server for {SilenceLimit} s");
                return;
            }
        }
    }
}
