using System;

namespace Internal
{
    // How PeerMesh connects the pairs of the session.
    public readonly struct PeerMeshOptions
    {
        public PeerMeshOptions(bool isDirectAllowed, TimeSpan reconnectDelay, TimeSpan maxReconnectDelay)
        {
            if (reconnectDelay <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(reconnectDelay), reconnectDelay,
                    "[Network] [Mesh] The reconnect delay is positive");

            if (maxReconnectDelay < reconnectDelay)
                throw new ArgumentOutOfRangeException(nameof(maxReconnectDelay), maxReconnectDelay,
                    "[Network] [Mesh] The ceiling of the reconnect delay is not below its first value");

            IsDirectAllowed = isDirectAllowed;
            ReconnectDelay = reconnectDelay;
            MaxReconnectDelay = maxReconnectDelay;
        }

        // False on a platform without a peer (WebRtcPeers.IsSupported) and in the tests of the tunnel: the pairs go
        // through the server at once, without a negotiation.
        public readonly bool IsDirectAllowed;

        // The pause before a new try of a lost connection. It doubles with every failure up to MaxReconnectDelay and
        // starts from ReconnectDelay again after a connection opens.
        public readonly TimeSpan ReconnectDelay;

        public readonly TimeSpan MaxReconnectDelay;
    }
}
