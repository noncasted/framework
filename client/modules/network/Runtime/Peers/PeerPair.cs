using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // One pair of the mesh: the connection to one other player, from the negotiation of a direct connection to the
    // fallback to the tunnel of the server and the retries after a loss. The smaller index offers, the bigger one
    // answers: the roles are fixed by the indices, so two peers never offer to each other.
    internal sealed class PeerPair
    {
        public PeerPair(
            ISessionControl control,
            int remoteIndex,
            WebRtcPeerOptions options,
            PeerMeshOptions meshOptions,
            IReadOnlyLifetime lifetime,
            Action<IPeerLink> onLink,
            Action<IPeerLink> onLost)
        {
            if (remoteIndex == control.LocalIndex)
                throw new InvalidOperationException($"[Network] [Mesh] A pair with the local index {remoteIndex}");

            _control = control;
            _remoteIndex = remoteIndex;
            _options = options;
            _meshOptions = meshOptions;
            _lifetime = lifetime.Child();
            _onLink = onLink;
            _onLost = onLost;
            _role = control.LocalIndex < remoteIndex ? WebRtcPeerRole.Offer : WebRtcPeerRole.Answer;
            _retry = new PeerRetry(_lifetime, meshOptions.ReconnectDelay, meshOptions.MaxReconnectDelay, OnRetryTime);
        }

        private readonly ISessionControl _control;
        private readonly int _remoteIndex;
        private readonly WebRtcPeerOptions _options;
        private readonly PeerMeshOptions _meshOptions;
        private readonly ILifetime _lifetime;
        private readonly Action<IPeerLink> _onLink;
        private readonly Action<IPeerLink> _onLost;
        private readonly WebRtcPeerRole _role;

        // How long the opening frame of the tunnel is repeated: a peer that joins right after the others is
        // authorized a little later, and the first hellos are lost.
        private const int HelloRepeats = 25;
        private readonly PeerRetry _retry;

        private PeerPairState _state;
        private Guid _remoteId;
        private ILifetime _attempt;
        private WebRtcPeerLink _direct;
        private IWebSocket _tunnel;
        private Func<Guid, IPeerLink> _readyLink;
        private PeerLinkInbox _inbox;
        private IPeerLink _link;

        // The player is in the session (ISessionControl.Peers): the id of the link and the end of the pair come from it.
        public void SetRemoteId(Guid remoteId)
        {
            _remoteId = remoteId;
            TryPublish();
        }

        public void Start()
        {
            if (_state != PeerPairState.Idle)
                return;

            Begin();
        }

        // The first offer of the player: the channel opened here takes the signal, inside the call of the signaling.
        public void OnOffer()
        {
            if (_state == PeerPairState.Gone || _role != WebRtcPeerRole.Answer)
                return;

            // The channel is open and the offer goes to it.
            if (_state == PeerPairState.ConnectingDirect)
                return;

            // The offering side starts a new connection: the local one (direct or through the tunnel) is stale.
            if (_state == PeerPairState.Connected)
                Lose("the peer negotiates again");

            BeginDirect();
        }

        // The first frame of a tunnel of the server: the other side of the pair chose it.
        public void OnTunnel()
        {
            if (_state == PeerPairState.Gone)
                return;

            if (_state == PeerPairState.Connected && _tunnel != null)
                return;

            if (_state == PeerPairState.Connected)
                Lose("the peer goes through the tunnel");

            BeginTunnel();
        }

        public void PeerGone()
        {
            if (_state == PeerPairState.Gone)
                return;

            _state = PeerPairState.Gone;
            ReportLost();
            CancelRetry();
            EndAttempt();
            _lifetime.Terminate();
        }

        private void Begin()
        {
            if (_meshOptions.IsDirectAllowed == true)
            {
                BeginDirect();
                return;
            }

            // The bigger index waits for the first frame of the tunnel: the smaller one opens it.
            if (_role == WebRtcPeerRole.Offer)
                BeginTunnel();
        }

        private void BeginDirect()
        {
            if (_state == PeerPairState.Gone || _meshOptions.IsDirectAllowed == false)
                return;

            CancelRetry();
            EndAttempt();

            var attempt = _lifetime.Child();
            var signaling = _control.Signaling.Open(attempt, _remoteIndex);
            var link = new WebRtcPeerLink(_role, signaling, _options, attempt);
            var inbox = new PeerLinkInbox(_options.Channels.Count, attempt);

            for (var i = 0; i < link.Channels.Count; i++)
            {
                var channel = i;
                link.Channels[i].Received.Advise(attempt, frame => inbox.Receive(channel, frame));
            }

            _attempt = attempt;
            _direct = link;
            _inbox = inbox;
            _state = PeerPairState.ConnectingDirect;
            link.Closed.Advise(attempt, info => OnDirectClosed(link, info));

            ConnectAsync(link, attempt).NoAwait();
        }

        private async UniTaskVoid ConnectAsync(WebRtcPeerLink link, ILifetime attempt)
        {
            try
            {
                await link.Connect();
            }
            catch (WebRtcConnectFailedException exception)
            {
                if (_state != PeerPairState.ConnectingDirect || _attempt != attempt)
                    return;

                Debug.LogWarning($"[Network] [Mesh] No direct connection with {_remoteIndex}: {exception.Message}");
                EndAttempt();
                FallbackAfterDirect(exception.Message);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_state != PeerPairState.ConnectingDirect || _attempt != attempt)
                return;

            _state = PeerPairState.Connected;
            _retry.Reset();
            var inbox = _inbox;
            _readyLink = id => new DirectPeerLink(link, inbox, _remoteIndex, id, attempt.Child());
            TryPublish();
        }

        // The direct connection did not open: the smaller index opens the tunnel, the bigger one waits for its first
        // frame (and tries the direct connection again if it does not come).
        private void FallbackAfterDirect(string reason)
        {
            if (_role == WebRtcPeerRole.Offer)
            {
                Debug.Log($"[Network] [Mesh] {_remoteIndex} goes through the tunnel: {reason}");
                BeginTunnel();
                return;
            }

            ScheduleRetry();
        }

        private void BeginTunnel()
        {
            if (_state == PeerPairState.Gone)
                return;

            CancelRetry();
            EndAttempt();

            var attempt = _lifetime.Child();
            var tunnel = _control.Tunnels.Open(attempt, _remoteIndex);
            var inbox = new PeerLinkInbox(_options.Channels.Count, attempt);

            TunnelPeerLink.Listen(tunnel, inbox, _remoteIndex, attempt);

            _attempt = attempt;
            _tunnel = tunnel;
            _inbox = inbox;
            _state = PeerPairState.Connected;
            tunnel.Closed.Advise(attempt, info => OnTunnelClosed(tunnel, info));

            // The first frame opens the tunnel on the other side: it belongs to no channel and carries no payload.
            if (_role == WebRtcPeerRole.Offer)
                RepeatHello(tunnel, attempt).NoAwait();

            _readyLink = id => new TunnelPeerLink(tunnel, inbox, _remoteIndex, id, attempt.Child());
            TryPublish();
        }

        // The server relays a hello only to a player whose connection is authorized: a hello sent before that is
        // dropped, so the opening frame is repeated for a while. The other side ignores the extra ones.
        private async UniTaskVoid RepeatHello(IWebSocket tunnel, IReadOnlyLifetime attempt)
        {
            var hellos = 0;

            try
            {
                while (attempt.IsTerminated == false && hellos < HelloRepeats)
                {
                    tunnel.Send(new ArraySegment<byte>(new[] { TunnelPeerLink.Hello })).NoAwait();
                    hellos++;

                    await UniTask.Delay(_meshOptions.ReconnectDelay, cancellationToken: attempt.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnDirectClosed(WebRtcPeerLink link, SocketCloseInfo info)
        {
            // Before the open the loss is the failure of ConnectAsync.
            if (_state != PeerPairState.Connected || _direct != link)
                return;

            Lose(info.ToString());
        }

        private void OnTunnelClosed(IWebSocket tunnel, SocketCloseInfo info)
        {
            if (_state != PeerPairState.Connected || _tunnel != tunnel)
                return;

            Lose(info.ToString());
        }

        private void Lose(string reason)
        {
            Debug.LogWarning($"[Network] [Mesh] The connection to {_remoteIndex} is lost: {reason}");
            ReportLost();
            EndAttempt();
            ScheduleRetry();
        }

        private void ReportLost()
        {
            if (_link == null)
                return;

            var link = _link;
            _link = null;
            _onLost(link);
        }

        private void ScheduleRetry()
        {
            if (_state == PeerPairState.Gone)
                return;

            _state = PeerPairState.WaitingRetry;
            _retry.Start();
        }

        private void OnRetryTime()
        {
            if (_state != PeerPairState.WaitingRetry)
                return;

            Debug.Log($"[Network] [Mesh] A new try of the connection to {_remoteIndex}");
            Begin();
        }

        private void CancelRetry()
        {
            _retry.Cancel();
        }

        private void EndAttempt()
        {
            _attempt?.Terminate();
            _attempt = null;
            _direct = null;
            _tunnel = null;
            _readyLink = null;
            _inbox = null;
        }

        // The link goes to the mesh once its connection is open and the id of the player is known (an offer may come
        // before the RemoteUpdate of the player). The frames received until then go to the subscribers that attached
        // when the link appeared.
        private void TryPublish()
        {
            if (_state != PeerPairState.Connected || _readyLink == null || _remoteId == Guid.Empty)
                return;

            var factory = _readyLink;
            var inbox = _inbox;
            _readyLink = null;
            _link = factory(_remoteId);
            Debug.Log($"[Network] [Mesh] Connected to {_remoteIndex} ({_remoteId}): {_link.Route}");
            _onLink(_link);
            inbox.Release();
        }
    }
}
