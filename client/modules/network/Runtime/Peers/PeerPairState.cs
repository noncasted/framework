namespace Internal
{
    // What one pair of the mesh is doing now.
    internal enum PeerPairState
    {
        // Nobody has started the connection yet: the player is not in Peers, or the bigger index waits for a tunnel.
        Idle,

        // The signaling channel is open and the direct connection is being negotiated.
        ConnectingDirect,

        // The link is open (direct or through the tunnel) and is in PeerMesh.Links once the id of the player is known.
        Connected,

        // The connection is lost: a new try follows after the pause of PeerRetry.
        WaitingRetry,

        // The player has left the session: nothing of the pair lives.
        Gone
    }
}
