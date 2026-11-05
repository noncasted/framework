namespace Internal
{
    // A data channel declared before the negotiation. Both sides declare the same list, and a channel is addressed by
    // its index in it; the label identifies the channel to the answering side.
    public readonly struct WebRtcChannelSpec
    {
        public WebRtcChannelSpec(string label, WebRtcChannelMode mode)
        {
            Label = label;
            Mode = mode;
        }

        // The label of the data channel (rtcCreateDataChannelEx), unique within one connection.
        public readonly string Label;

        public readonly WebRtcChannelMode Mode;

        public override string ToString()
        {
            return $"{Label} ({Mode})";
        }
    }
}
