using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // The host of a P2P match accepts its guests on the local server that runs the session of the match: through a
    // direct data channel (the answer to the negotiation a guest starts) or through the tunnel of the server (the first
    // frame of a guest without a direct connection).
    public class PeerHost
    {
        public PeerHost(ISessionControl control, LocalSessionServer server, IPeerMatch match, WebRtcPeerOptions options)
        {
            _control = control;
            _server = server;
            _match = match;
            _options = options;
        }

        private readonly ISessionControl _control;
        private readonly LocalSessionServer _server;
        private readonly IPeerMatch _match;
        private readonly WebRtcPeerOptions _options;

        public void Run(IReadOnlyLifetime lifetime)
        {
            // A platform without a peer leaves the offers unanswered: the guest goes through the tunnel by its timeout.
            if (WebRtcPeers.IsSupported(Application.platform) == true)
                _control.Signaling.Incoming.Advise(lifetime, index => AcceptDirect(lifetime, index).Forget());

            _control.Tunnels.Incoming.Advise(lifetime, index => AcceptTunnel(lifetime, index));
        }

        // The channel of the negotiation is opened inside the call: the offer that raised it goes to the socket.
        private async UniTaskVoid AcceptDirect(IReadOnlyLifetime lifetime, int index)
        {
            var attempt = lifetime.Child();

            try
            {
                var socket = new WebRtcPeerSocket(WebRtcPeerRole.Answer, _control.Signaling.Open(attempt, index), _options, attempt);

                // Before the open: the open and the first frame of the guest may come in one frame of the host, and a
                // lost channel ends the attempt, so a returning guest negotiates anew.
                socket.Closed.Advise(attempt, _ => attempt.Terminate());
                _server.AcceptSocket(attempt, socket);

                await socket.Connect();
            }
            // The guest goes through the tunnel then.
            catch (WebRtcConnectFailedException exception)
            {
                Debug.LogWarning($"[Network] [Peers] No direct connection with the guest {index}: {exception.Message}");
                attempt.Terminate();
                return;
            }
            // Any other end frees the channel of the guest as well: it stays taken otherwise.
            catch
            {
                attempt.Terminate();
                throw;
            }

            Debug.Log($"[Network] [Peers] Guest {index} connected directly");
            _match.SetRoute(index, PeerRoute.Direct);
        }

        private void AcceptTunnel(IReadOnlyLifetime lifetime, int index)
        {
            Debug.Log($"[Network] [Peers] Guest {index} connects through the tunnel");
            _match.SetRoute(index, PeerRoute.Tunnel);
            _server.AcceptSocket(lifetime, _control.Tunnels.Open(lifetime, index));
        }
    }
}
