using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // A WebRTC connection to another player with several data channels: the negotiation through IWebRtcSignaling, the
    // events of the platform peer queued to the main thread, the open of every channel, the framing of a reliable
    // channel. WebRtcPeerSocket is a link of one channel as an IWebSocket; the mesh of the constructor takes the
    // channels themselves. Connect ends when every channel is open and fails (the caller falls back to the server) when
    // the negotiation does not open them within the timeout or the connection fails.
    public class WebRtcPeerLink : IWebRtcChannelEvents
    {
        public WebRtcPeerLink(
            WebRtcPeerRole role,
            IWebRtcSignaling signaling,
            WebRtcPeerOptions options,
            IReadOnlyLifetime lifetime)
        {
            _role = role;
            _signaling = signaling;
            _options = options;
            _lifetime = lifetime.Child();
            _inbox = new SocketInbox<WebRtcLinkEvent>(_lifetime, Handle);

            _channels = new WebRtcLinkChannel[options.Channels.Count];

            for (var i = 0; i < _channels.Length; i++)
                _channels[i] = new WebRtcLinkChannel(this, i, options.Channels[i]);

            _peer = WebRtcPeers.CreateChannels(options, this);
            _lifetime.Listen(Dispose);

            // From the start: the offer of the peer may come before Connect.
            _signaling.Received.Advise(_lifetime, OnSignal);
            SocketPump.Run(_lifetime, _inbox.Drain).NoAwait();
        }

        private readonly WebRtcPeerRole _role;
        private readonly IWebRtcSignaling _signaling;
        private readonly WebRtcPeerOptions _options;
        private readonly ILifetime _lifetime;
        private readonly IWebRtcChannels _peer;
        private readonly WebRtcLinkChannel[] _channels;
        private readonly ViewableDelegate<SocketCloseInfo> _closed = new();

        // The events of the peer, from the threads of the platform, in their order.
        private readonly SocketInbox<WebRtcLinkEvent> _inbox;

        // Candidates of the peer that came before its description: applied once it is set.
        private readonly List<WebRtcSignal> _pendingCandidates = new();

        // Candidates of the offering side, kept until the answer is set. The library sets a description on its ICE agent
        // first and stores it for the check of the DTLS certificate after that: a peer that already has these candidates
        // connects in between, the check finds no description and the handshake fails.
        private readonly List<WebRtcSignal> _heldCandidates = new();

        private bool _isRemoteDescriptionSet;
        private bool _isOpen;
        private bool _isConnecting;
        private bool _isClosedNotified;
        private string _lostReason;

        public IReadOnlyList<IWebRtcLinkChannel> Channels => _channels;
        public IViewableDelegate<SocketCloseInfo> Closed => _closed;

        public async UniTask Connect()
        {
            if (_isConnecting == true)
                throw new InvalidOperationException("[Network] [WebRtc] Connect is called twice");

            _isConnecting = true;

            if (_role == WebRtcPeerRole.Offer)
                _peer.CreateChannels();

            var isTimedOut = await UniTask
                                   .WaitUntil(() => _isOpen == true || _lostReason != null,
                                       cancellationToken: _lifetime.Token)
                                   .TimeoutWithoutException(_options.ConnectTimeout, DelayType.Realtime);

            // In this order: TimeoutWithoutException reports a cancellation as a timeout, and a loss right after the open
            // ends the lifetime before this wakes up. Only the end of the lifetime from outside is not a failed channel.
            if (_lostReason != null)
                throw new WebRtcConnectFailedException(_lostReason);

            _lifetime.Token.ThrowIfCancellationRequested();

            if (isTimedOut == true)
                throw new WebRtcConnectFailedException($"the channels did not open in {_options.ConnectTimeout.TotalSeconds} s");

            Debug.Log($"[Network] [WebRtc] Connected: {_role}, {DescribeChannels()}");
        }

        public void Drop(string reason)
        {
            _lostReason ??= $"dropped: {reason}";
            NotifyClosed();
        }

        void IWebRtcChannelEvents.OnLocalDescription(string type, string sdp)
        {
            _inbox.Enqueue(WebRtcLinkEvent.OfSignal(WebRtcSignal.Description(type, sdp)));
        }

        void IWebRtcChannelEvents.OnLocalCandidate(string candidate, string mid)
        {
            _inbox.Enqueue(WebRtcLinkEvent.OfSignal(WebRtcSignal.Candidate(candidate, mid)));
        }

        void IWebRtcChannelEvents.OnOpen()
        {
            _inbox.Enqueue(WebRtcLinkEvent.OfOpen());
        }

        void IWebRtcChannelEvents.OnMessage(int channel, byte[] bytes)
        {
            _inbox.Enqueue(WebRtcLinkEvent.OfMessage(channel, bytes));
        }

        void IWebRtcChannelEvents.OnLost(string reason)
        {
            _inbox.Enqueue(WebRtcLinkEvent.OfLost(reason));
        }

        // A channel of this link sends a frame: a lost or closed connection drops it as every other one left in the queue
        // of the socket, the sender may be a loop over the other players.
        internal void Send(WebRtcLinkChannel linkChannel, ArraySegment<byte> bytes)
        {
            if (_lostReason != null || _lifetime.IsTerminated == true)
                return;

            if (_isOpen == false)
                throw new InvalidOperationException("[Network] [WebRtc] Send before the channel is open");

            // The boundary of the network: the channel closed and its Lost is still in the queue.
            try
            {
                linkChannel.SendOpen(bytes);
            }
            catch (Exception exception)
            {
                Lose($"send failed: {exception.Message}");
            }
        }

        internal void SendRaw(int channel, byte[] message)
        {
            _peer.Send(channel, message);
        }

        internal int BufferedAmount(int channel)
        {
            return _peer.BufferedAmount(channel);
        }

        private void OnSignal(WebRtcSignal signal)
        {
            // The rest of the signals of a connection that is gone.
            if (_lostReason != null)
                return;

            // The boundary of the network: the peer closed and its Lost is still in the queue, or the other side sent
            // what the peer does not take. Either way this connection is lost, as when a send fails.
            try
            {
                ApplySignal(signal);
            }
            catch (Exception exception)
            {
                Lose($"signal failed: {exception.Message}");
                return;
            }

            if (signal.Kind == WebRtcSignalKind.Description)
                SendHeldCandidates();
        }

        private void ApplySignal(WebRtcSignal signal)
        {
            switch (signal.Kind)
            {
                case WebRtcSignalKind.Description:
                    _peer.SetRemoteDescription(signal.Type, signal.Value);
                    _isRemoteDescriptionSet = true;

                    foreach (var candidate in _pendingCandidates)
                        _peer.AddRemoteCandidate(candidate.Value, candidate.Mid);

                    _pendingCandidates.Clear();
                    break;
                case WebRtcSignalKind.Candidate:
                    if (_isRemoteDescriptionSet == true)
                        _peer.AddRemoteCandidate(signal.Value, signal.Mid);
                    else
                        _pendingCandidates.Add(signal);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(signal), signal.Kind, null);
            }
        }

        private void Handle(WebRtcLinkEvent linkEvent)
        {
            switch (linkEvent.EventKind)
            {
                case WebRtcLinkEvent.Kind.Signal:
                    SendLocalSignal(linkEvent.Signal);
                    break;
                case WebRtcLinkEvent.Kind.Open:
                    // Here, not in Connect: a frame in the same pass is answered before Connect wakes up.
                    foreach (var channel in _channels)
                        channel.Opened(_peer.MaxMessageSize(channel.Index));

                    _isOpen = true;
                    break;
                case WebRtcLinkEvent.Kind.Message:
                    var linkChannel = _channels[linkEvent.Channel];
                    var frame = linkChannel.ReceivedFrame(linkEvent.Bytes);

                    if (frame != null)
                        linkChannel.RaiseReceived(frame);
                    break;
                case WebRtcLinkEvent.Kind.Lost:
                    _lostReason ??= linkEvent.Reason;

                    // Before the channels open the loss is the failure of Connect, not a loss of a connection.
                    if (_isOpen == true)
                        NotifyClosed();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(linkEvent), linkEvent.EventKind, null);
            }
        }

        private void SendLocalSignal(WebRtcSignal signal)
        {
            if (_role == WebRtcPeerRole.Offer && signal.Kind == WebRtcSignalKind.Candidate && _isRemoteDescriptionSet == false)
                _heldCandidates.Add(signal);
            else
                _signaling.Send(signal);
        }

        private void SendHeldCandidates()
        {
            foreach (var candidate in _heldCandidates)
                _signaling.Send(candidate);

            _heldCandidates.Clear();
        }

        private void Lose(string reason)
        {
            Debug.LogWarning($"[Network] [WebRtc] {reason}");
            _lostReason ??= reason;
            _inbox.Enqueue(WebRtcLinkEvent.OfLost(_lostReason));
        }

        private void NotifyClosed()
        {
            if (_isClosedNotified == true || _lifetime.IsTerminated == true)
                return;

            _isClosedNotified = true;

            var info = new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, $"peer lost: {_lostReason}");
            Debug.LogWarning($"[Network] [WebRtc] Connection lost: {info}");

            _closed.Invoke(info);
            _lifetime.Terminate();
        }

        private void Dispose()
        {
            _peer.Dispose();

            foreach (var channel in _channels)
                channel.Dispose();

            _closed.Dispose();
        }

        private string DescribeChannels()
        {
            var text = new StringBuilder();

            foreach (var channel in _channels)
            {
                if (text.Length > 0)
                    text.Append(", ");

                text.Append(channel.Spec);
            }

            return text.ToString();
        }

    }
}
