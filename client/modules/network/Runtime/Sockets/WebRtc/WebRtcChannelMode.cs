namespace Internal
{
    // How a data channel of a peer connection delivers its messages.
    public enum WebRtcChannelMode
    {
        // A reliable ordered stream: every message arrives, in the order it was sent (records, media).
        ReliableOrdered = 0,

        // Unreliable unordered messages: a lost one is gone, and a later one may arrive first (presence).
        Unreliable = 1
    }
}
