namespace Internal
{
    // A message of the WebRTC negotiation between two peers: a session description (offer / answer) or an ICE
    // candidate. Carried by the signaling of the session (IWebRtcSignaling), never by the data channel itself.
    public readonly struct WebRtcSignal
    {
        private WebRtcSignal(WebRtcSignalKind kind, string type, string value, string mid)
        {
            Kind = kind;
            Type = type;
            Value = value;
            Mid = mid;
        }

        public readonly WebRtcSignalKind Kind;

        // Description: "offer" / "answer". Empty for a candidate.
        public readonly string Type;

        // Description: the SDP. Candidate: the candidate line.
        public readonly string Value;

        // Candidate: the media id it belongs to. Empty for a description.
        public readonly string Mid;

        public static WebRtcSignal Description(string type, string sdp)
        {
            return new WebRtcSignal(WebRtcSignalKind.Description, type, sdp, string.Empty);
        }

        public static WebRtcSignal Candidate(string candidate, string mid)
        {
            return new WebRtcSignal(WebRtcSignalKind.Candidate, string.Empty, candidate, mid);
        }

        public override string ToString()
        {
            return Kind == WebRtcSignalKind.Description ? $"description {Type}" : $"candidate {Value}";
        }
    }
}
