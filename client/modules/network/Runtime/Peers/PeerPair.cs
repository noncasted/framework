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
            _role = control.LocalIndex < remoteIndex ? WebRtcPeerRole.Offer : WebRtcPeerRole.Answer;
            _retry = new PeerRetry(_lifetime, meshOptions.ReconnectDelay, meshOptions.MaxReconnectDelay, OnRetryTime);
            _publication = new PeerLinkPublication(remoteIndex, onLink, onLost);
        }

        private readonly ISessionControl _control;
        private readonly int _remoteIndex;
        private readonly WebRtcPeerOptions _options;
        private readonly PeerMeshOptions _meshOptions;
        private readonly ILifetime _lifetime;
        private readonly WebRtcPeerRole _role;
        private readonly PeerRetry _retry;
        private readonly PeerLinkPublication _publication;

        private PeerPairState _state;
        private Guid _remoteId;
        private ILifetime _attempt;
        private WebRtcPeerLink _direct;
        private IWebSocket _tunnel;

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
            // Without direct connections the pair has opened the tunnel itself: its hello moves the offering side there.
            if (_state == PeerPairState.Gone || _role != WebRtcPeerRole.Answer || _meshOptions.IsDirectAllowed == false)
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
            _publication.ReportLost();
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

            // A player without direct connections opens the tunnel whatever its role: the other side may offer a direct
            // connection and would wait for its timeout before the fallback. The hello moves it to the tunnel at once.
            BeginTunnel();
        }

        private void BeginDirect()
        {
            if (_state == PeerPairState.Gone || _meshOptions.IsDirectAllowed == false)
                return;

            CancelRetry();
            EndAttempt();

            // The attempt is the current one at once: a link that fails to build ends it with its signaling channel.
            var attempt = _lifetime.Child();
            _attempt = attempt;

            WebRtcPeerLink link;

            try
            {
                link = new WebRtcPeerLink(_role, _control.Signaling.Open(attempt, _remoteIndex), _options, attempt);
            }
            // The platform can't build the connection (no peer, a layout of channels it does not carry): the pair
            // plays through the server as after a failed negotiation.
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EndAttempt();
                FallbackAfterDirect($"the direct connection can't be created: {exception.Message}");
                return;
            }

            var inbox = new PeerLinkInbox(_options.Channels.Count, attempt);

            for (var i = 0; i < link.Channels.Count; i++)
            {
                var channel = i;
                link.Channels[i].Received.Advise(attempt, frame => inbox.Receive(channel, frame));
            }

            _direct = link;
            _state = PeerPairState.ConnectingDirect;
            link.Closed.Advise(attempt, info => OnDirectClosed(link, info));

            ConnectAsync(link, inbox, attempt).NoAwait();
        }

        private async UniTaskVoid ConnectAsync(WebRtcPeerLink link, PeerLinkInbox inbox, ILifetime attempt)
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
            _publication.Prepare(id => new DirectPeerLink(link, inbox, _remoteIndex, id, attempt.Child()), inbox);
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
            _state = PeerPairState.Connected;
            tunnel.Closed.Advise(attempt, info => OnTunnelClosed(tunnel, info));

            // The offering side opens the tunnel after a failed direct connection; a player without direct connections
            // opens it for any role.
            if (_role == WebRtcPeerRole.Offer || _meshOptions.IsDirectAllowed == false)
                PeerTunnelHello.Repeat(tunnel, _meshOptions.ReconnectDelay, attempt).NoAwait();

            _publication.Prepare(id => new TunnelPeerLink(tunnel, inbox, _remoteIndex, id, attempt.Child()), inbox);
            TryPublish();
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
            _publication.ReportLost();
            EndAttempt();
            ScheduleRetry();
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
            _publication.Clear();
        }

        // The link goes to the mesh once its connection is open and the id of the player is known.
        private void TryPublish()
        {
            if (_state == PeerPairState.Connected)
                _publication.TryPublish(_remoteId);
        }
    }
}
