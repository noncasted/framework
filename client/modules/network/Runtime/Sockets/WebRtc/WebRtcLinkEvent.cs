namespace Internal
{
    // What WebRtcPeerLink receives from the threads of the platform peer, in its order.
    internal readonly struct WebRtcLinkEvent
    {
        public enum Kind
        {
            Signal,
            Open,
            Message,
            Lost
        }

        private WebRtcLinkEvent(Kind kind, WebRtcSignal signal, int channel, byte[] bytes, string reason)
        {
            EventKind = kind;
            Signal = signal;
            Channel = channel;
            Bytes = bytes;
            Reason = reason;
        }

        public readonly Kind EventKind;
        public readonly WebRtcSignal Signal;
        public readonly int Channel;
        public readonly byte[] Bytes;
        public readonly string Reason;

        public static WebRtcLinkEvent OfSignal(WebRtcSignal signal) => new(Kind.Signal, signal, 0, null, null);
        public static WebRtcLinkEvent OfOpen() => new(Kind.Open, default, 0, null, null);
        public static WebRtcLinkEvent OfMessage(int channel, byte[] bytes) => new(Kind.Message, default, channel, bytes, null);
        public static WebRtcLinkEvent OfLost(string reason) => new(Kind.Lost, default, 0, null, reason);
    }
}
