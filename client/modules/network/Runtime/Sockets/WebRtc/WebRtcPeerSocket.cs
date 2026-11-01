using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // IWebSocket over a WebRTC data channel to another player: the same reliable ordered stream of frames as the socket
    // to the server, so nothing above the socket (reader, writer, commands, session) knows the difference. The two
    // sides negotiate through IWebRtcSignaling; Connect ends when the channel is open, and fails (the caller falls back
    // to the server) when the negotiation does not open it within the timeout or the connection fails.
    public class WebRtcPeerSocket : IWebSocket, IWebRtcPeerEvents
    {
        public WebRtcPeerSocket(
            WebRtcPeerRole role,
            IWebRtcSignaling signaling,
            WebRtcPeerOptions options,
            IReadOnlyLifetime lifetime)
        {
            _role = role;
            _signaling = signaling;
            _options = options;
            _lifetime = lifetime.Child();

            _peer = WebRtcPeers.Create(options, this);
            _lifetime.Listen(Dispose);

            // From the start: the offer of the peer may come before Connect.
            _signaling.Received.Advise(_lifetime, OnSignal);
            Pump().Forget();
        }

        private const string ChannelLabel = "session";

        private readonly WebRtcPeerRole _role;
        private readonly IWebRtcSignaling _signaling;
        private readonly WebRtcPeerOptions _options;
        private readonly ILifetime _lifetime;
        private readonly IWebRtcPeer _peer;
        private readonly ViewableDelegate<byte[]> _received = new();
        private readonly ViewableDelegate<SocketCloseInfo> _closed = new();
        private readonly WebRtcFrameAssembler _assembler = new();

        // The events of the peer, from the threads of the platform, in their order.
        private readonly ConcurrentQueue<PeerEvent> _inbox = new();

        // Candidates of the peer that came before its description: applied once it is set.
        private readonly List<WebRtcSignal> _pendingCandidates = new();

        private bool _isRemoteDescriptionSet;
        private bool _isOpen;
        private bool _isConnecting;
        private bool _isClosedNotified;
        private string _lostReason;
        private int _payloadSize;

        public IViewableDelegate<byte[]> Received => _received;
        public IViewableDelegate<SocketCloseInfo> Closed => _closed;

        public async UniTask Connect()
        {
            if (_isConnecting == true)
                throw new InvalidOperationException("[Network] [WebRtc] Connect is called twice");

            _isConnecting = true;

            if (_role == WebRtcPeerRole.Offer)
                _peer.CreateChannel(ChannelLabel);

            var isTimedOut = await UniTask
                                   .WaitUntil(() => _isOpen == true || _lostReason != null,
                                       cancellationToken: _lifetime.Token)
                                   .TimeoutWithoutException(_options.ConnectTimeout, DelayType.Realtime);

            if (isTimedOut == true)
                throw new WebRtcConnectFailedException($"the channel did not open in {_options.ConnectTimeout.TotalSeconds} s");

            if (_isOpen == false)
                throw new WebRtcConnectFailedException(_lostReason);

            Debug.Log($"[Network] [WebRtc] Connected: {_role}, part payload {_payloadSize} B");
        }

        public UniTask Send(byte[] bytes)
        {
            // Lost: the loss is reported through Closed, the frame is dropped as every other one left in the queue of
            // a lost connection - as the other sockets do.
            if (_lostReason != null || _lifetime.IsTerminated == true)
                return UniTask.CompletedTask;

            if (_isOpen == false)
                throw new InvalidOperationException("[Network] [WebRtc] Send before the channel is open");

            // The boundary of the network: the channel closed and its Lost is still in the queue. The sender may be a loop
            // over the other players, so the failure goes the way of every other loss.
            try
            {
                WebRtcFraming.Split(bytes, _payloadSize, _peer.Send);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Network] [WebRtc] Send failed: {exception.Message}");
                _lostReason ??= $"send failed: {exception.Message}";
                _inbox.Enqueue(PeerEvent.OfLost(_lostReason));
            }

            return UniTask.CompletedTask;
        }

        public void Drop(string reason)
        {
            _lostReason ??= $"dropped: {reason}";
            NotifyClosed();
        }

        void IWebRtcPeerEvents.OnLocalDescription(string type, string sdp)
        {
            _inbox.Enqueue(PeerEvent.OfSignal(WebRtcSignal.Description(type, sdp)));
        }

        void IWebRtcPeerEvents.OnLocalCandidate(string candidate, string mid)
        {
            _inbox.Enqueue(PeerEvent.OfSignal(WebRtcSignal.Candidate(candidate, mid)));
        }

        void IWebRtcPeerEvents.OnOpen()
        {
            _inbox.Enqueue(PeerEvent.OfOpen());
        }

        void IWebRtcPeerEvents.OnMessage(byte[] bytes)
        {
            _inbox.Enqueue(PeerEvent.OfMessage(bytes));
        }

        void IWebRtcPeerEvents.OnLost(string reason)
        {
            _inbox.Enqueue(PeerEvent.OfLost(reason));
        }

        private void OnSignal(WebRtcSignal signal)
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

        // The events of the peer handed over on the main thread once a frame, as DefaultWebSocket delivers its frames.
        private async UniTaskVoid Pump()
        {
            while (true)
            {
                var isCanceled = await UniTask
                                       .Yield(PlayerLoopTiming.Update, _lifetime.Token)
                                       .SuppressCancellationThrow();

                if (isCanceled == true)
                    return;

                while (_lifetime.IsTerminated == false && _inbox.TryDequeue(out var peerEvent) == true)
                    Handle(peerEvent);
            }
        }

        private void Handle(PeerEvent peerEvent)
        {
            switch (peerEvent.Kind)
            {
                case PeerEventKind.Signal:
                    _signaling.Send(peerEvent.Signal);
                    break;
                case PeerEventKind.Open:
                    // Here, not in Connect: a frame in the same pass is answered before Connect wakes up.
                    _payloadSize = WebRtcFraming.PayloadSize(_peer.MaxMessageSize);
                    _isOpen = true;
                    break;
                case PeerEventKind.Message:
                    var frame = _assembler.Add(peerEvent.Bytes);

                    if (frame != null)
                        _received.Invoke(frame);
                    break;
                case PeerEventKind.Lost:
                    _lostReason ??= peerEvent.Reason;

                    // Before the channel opens the loss is the failure of Connect, not a loss of a connection.
                    if (_isOpen == true)
                        NotifyClosed();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(peerEvent), peerEvent.Kind, null);
            }
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
            _received.Dispose();
            _closed.Dispose();
        }

        private enum PeerEventKind
        {
            Signal,
            Open,
            Message,
            Lost
        }

        private readonly struct PeerEvent
        {
            private PeerEvent(PeerEventKind kind, WebRtcSignal signal, byte[] bytes, string reason)
            {
                Kind = kind;
                Signal = signal;
                Bytes = bytes;
                Reason = reason;
            }

            public readonly PeerEventKind Kind;
            public readonly WebRtcSignal Signal;
            public readonly byte[] Bytes;
            public readonly string Reason;

            public static PeerEvent OfSignal(WebRtcSignal signal) => new(PeerEventKind.Signal, signal, null, null);
            public static PeerEvent OfOpen() => new(PeerEventKind.Open, default, null, null);
            public static PeerEvent OfMessage(byte[] bytes) => new(PeerEventKind.Message, default, bytes, null);
            public static PeerEvent OfLost(string reason) => new(PeerEventKind.Lost, default, null, reason);
        }
    }

    // Which side starts the negotiation: the offering side creates the channel, the answering one accepts it.
    public enum WebRtcPeerRole
    {
        Offer,
        Answer
    }

    // WebRtcPeerSocket.Connect: the direct connection did not open. An expected outcome: the caller goes through the
    // server instead.
    public class WebRtcConnectFailedException : Exception
    {
        public WebRtcConnectFailedException(string reason) : base($"[Network] [WebRtc] The peer did not connect: {reason}")
        {
        }
    }
}
