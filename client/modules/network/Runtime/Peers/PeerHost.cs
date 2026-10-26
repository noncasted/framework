using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // The host of a P2P match accepts its guests on the local server that runs the session of the match: through a
    // direct data channel (the answer to the negotiation a guest starts) or through the tunnel of the server (the first
    // frame of a guest without a direct connection).
    public class PeerHost
    {
        public PeerHost(ISessionControl control, LocalSessionServer server, WebRtcPeerOptions options)
        {
            _control = control;
            _server = server;
            _options = options;
        }

        private readonly ISessionControl _control;
        private readonly LocalSessionServer _server;
        private readonly WebRtcPeerOptions _options;

        public void Run(IReadOnlyLifetime lifetime)
        {
            _control.Signaling.Incoming.Advise(lifetime, index => AcceptDirect(lifetime, index).Forget());
            _control.Tunnels.Incoming.Advise(lifetime, index => AcceptTunnel(lifetime, index));
        }

        // The channel of the negotiation is opened inside the call: the offer that raised it goes to the socket.
        private async UniTaskVoid AcceptDirect(IReadOnlyLifetime lifetime, int index)
        {
            var attempt = lifetime.Child();
            var socket = new WebRtcPeerSocket(WebRtcPeerRole.Answer, _control.Signaling.Open(attempt, index), _options, attempt);

            try
            {
                await socket.Connect();
            }
            // The guest goes through the tunnel then.
            catch (WebRtcConnectFailedException exception)
            {
                Debug.LogWarning($"[Network] [Peers] No direct connection with the guest {index}: {exception.Message}");
                attempt.Terminate();
                return;
            }

            Debug.Log($"[Network] [Peers] Guest {index} connected directly");

            // A lost channel ends the attempt: a returning guest negotiates anew.
            socket.Closed.Advise(attempt, _ => attempt.Terminate());
            _server.AcceptSocket(attempt, socket);
        }

        private void AcceptTunnel(IReadOnlyLifetime lifetime, int index)
        {
            Debug.Log($"[Network] [Peers] Guest {index} connects through the tunnel");
            _server.AcceptSocket(lifetime, _control.Tunnels.Open(lifetime, index));
        }
    }
}
