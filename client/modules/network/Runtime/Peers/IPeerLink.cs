using System;

namespace Internal
{
    // A connection of the mesh to another player of the session: the data channels of WebRtcPeerLink (a direct
    // connection) or the tunnel of the server (a pair without one). The channels are the ones of
    // WebRtcPeerOptions.Channels, addressed by their index; whole frames in and out, on the main thread.
    public interface IPeerLink
    {
        // The index of the player in the server session.
        int RemoteIndex { get; }

        // The id of the player in the server session.
        Guid RemoteId { get; }

        PeerRoute Route { get; }

        // Ends when the connection is lost or the player leaves the session: the link leaves PeerMesh.Links.
        IReadOnlyLifetime Lifetime { get; }

        void Send(int channel, ArraySegment<byte> bytes);

        IViewableDelegate<byte[]> Received(int channel);

        // The bytes of the channel queued to send: 0 for a tunnel, which has no buffer of its own.
        int BufferedAmount(int channel);
    }
}
