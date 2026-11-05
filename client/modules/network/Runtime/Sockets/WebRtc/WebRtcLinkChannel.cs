using System;

namespace Internal
{
    // One channel of WebRtcPeerLink: the framing of a reliable channel (WebRtcFraming), the raw messages of an
    // unreliable one, and the state of its open.
    internal sealed class WebRtcLinkChannel : IWebRtcLinkChannel
    {
        public WebRtcLinkChannel(WebRtcPeerLink link, int index, WebRtcChannelSpec spec)
        {
            _link = link;
            Index = index;
            Spec = spec;
        }

        private readonly WebRtcPeerLink _link;
        private readonly ViewableDelegate<byte[]> _received = new();
        private readonly WebRtcFrameAssembler _assembler = new();

        private int _maxMessageSize;
        private int _payloadSize;
        private bool _isOpen;

        public int Index { get; }
        public WebRtcChannelSpec Spec { get; }
        public IViewableDelegate<byte[]> Received => _received;
        public int BufferedAmount => _link.BufferedAmount(Index);

        public void Send(ArraySegment<byte> bytes)
        {
            // An unreliable channel does not carry a frame in parts: a lost part would leave the receiver with a broken
            // frame, and nothing on an unordered channel would say which parts belong together.
            if (_isOpen == true && Spec.Mode == WebRtcChannelMode.Unreliable && bytes.Count > _maxMessageSize)
                throw new InvalidOperationException(
                    $"[Network] [WebRtc] A message of {bytes.Count} B does not fit the unreliable channel '{Spec.Label}' ({_maxMessageSize} B)");

            _link.Send(this, bytes);
        }

        internal void Opened(int maxMessageSize)
        {
            _maxMessageSize = maxMessageSize;
            _payloadSize = WebRtcFraming.PayloadSize(maxMessageSize);
            _isOpen = true;
        }

        // Sends a frame of a channel that is open: the link has checked the state and turns a failure into the loss of
        // the connection.
        internal void SendOpen(ArraySegment<byte> bytes)
        {
            if (Spec.Mode == WebRtcChannelMode.ReliableOrdered)
                WebRtcFraming.Split(bytes.ToArray(), _payloadSize, part => _link.SendRaw(Index, part));
            else
                _link.SendRaw(Index, bytes.ToArray());
        }

        // The frame a message completes, null while a reliable frame goes on. An unreliable message is a frame itself.
        internal byte[] ReceivedFrame(byte[] message)
        {
            return Spec.Mode == WebRtcChannelMode.Unreliable ? message : _assembler.Add(message);
        }

        internal void RaiseReceived(byte[] frame)
        {
            _received.Invoke(frame);
        }

        internal void Dispose()
        {
            _received.Dispose();
        }
    }
}
