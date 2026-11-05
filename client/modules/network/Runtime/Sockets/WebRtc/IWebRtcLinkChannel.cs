using System;

namespace Internal
{
    // One channel of a WebRtcPeerLink, on the main thread: whole frames in and out, the spec of the channel.
    public interface IWebRtcLinkChannel
    {
        // The index in WebRtcPeerOptions.Channels.
        int Index { get; }

        WebRtcChannelSpec Spec { get; }

        // Whole frames: a reliable channel splits and joins them (WebRtcFraming), an unreliable one carries a message as
        // it is. Invoked on the main thread.
        IViewableDelegate<byte[]> Received { get; }

        // The bytes of this channel queued to send, as the platform reports them.
        int BufferedAmount { get; }

        void Send(ArraySegment<byte> bytes);
    }
}
