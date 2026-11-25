using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // A connection lost without a close (a dropped network) is noticed by the socket only after minutes. A server that
    // answers the pings of the client every half a second is never silent for long, idle or not: a server silent for
    // Limit is a loss, the connection is dropped (INetworkConnection.Drop) and Closed goes to the code that handles
    // losses. The time is the frame time capped per frame (FrameDelta): a stalled main thread (a paused editor, a long
    // load) is not a silence of the server. The pings are the caller's: NetworkLatencyMonitor in a session of the game,
    // SessionControl on the control connection of a P2P match.
    public static class ConnectionSilence
    {
        public const float Limit = 10f;

        private const float MaxFrameDelta = 0.25f;

        private static readonly ILog _log = Logs.Of(typeof(ConnectionSilence));

        public static float FrameDelta => Mathf.Min(Time.unscaledDeltaTime, MaxFrameDelta);

        // Starts once the server answers the pings (it knows this user); ends with the lifetime or the drop.
        public static async UniTask Watch(IReadOnlyLifetime lifetime, INetworkConnection connection)
        {
            var received = connection.Traffic.ReceivedMessages;
            var silence = 0f;

            while (lifetime.IsTerminated == false)
            {
                await UniTask.Yield(lifetime.Token);

                var current = connection.Traffic.ReceivedMessages;

                if (current != received)
                {
                    received = current;
                    silence = 0f;
                    continue;
                }

                silence += FrameDelta;

                if (silence < Limit)
                    continue;

                _log.Warning("No message from the server for {Limit} s: the connection is lost", Limit);
                connection.Drop($"no message from the server for {Limit} s");
                return;
            }
        }
    }
}
