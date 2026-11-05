using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The first frame of a tunnel opens it on the other side: it belongs to no channel and carries no payload. The
    // server relays a hello only to a player whose connection is authorized: a hello sent before that is dropped, so
    // the opening frame is repeated for a while. The other side ignores the extra ones.
    internal static class PeerTunnelHello
    {
        // How long the opening frame is repeated: a peer that joins right after the others is authorized a little
        // later, and the first hellos are lost.
        private const int Repeats = 25;

        public static async UniTaskVoid Repeat(IWebSocket tunnel, TimeSpan interval, IReadOnlyLifetime attempt)
        {
            var hellos = 0;

            try
            {
                while (attempt.IsTerminated == false && hellos < Repeats)
                {
                    tunnel.Send(new ArraySegment<byte>(new[] { TunnelPeerLink.Hello })).NoAwait();
                    hellos++;

                    await UniTask.Delay(interval, cancellationToken: attempt.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
