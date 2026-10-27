using System;
using System.Collections.Generic;

namespace Internal
{
    // The P2P match the session of the game belongs to, for NetworkLatencyMonitor: the host and the route of every guest
    // to it. HostIndex 0 - the session is on the server.
    public interface IPeerMatch
    {
        int HostIndex { get; }

        // By the index of the guest: the local one on a guest, every connected guest on the host. A guest that comes
        // back takes its new route.
        IReadOnlyDictionary<int, PeerRoute> Routes { get; }

        void SetHost(int hostIndex);
        void SetRoute(int guestIndex, PeerRoute route);
    }

    public class PeerMatch : IPeerMatch
    {
        private readonly Dictionary<int, PeerRoute> _routes = new();

        public int HostIndex { get; private set; }
        public IReadOnlyDictionary<int, PeerRoute> Routes => _routes;

        public void SetHost(int hostIndex)
        {
            if (HostIndex != 0)
                throw new InvalidOperationException($"[Network] [Peers] The host is already set: {HostIndex}");

            HostIndex = hostIndex;
        }

        public void SetRoute(int guestIndex, PeerRoute route)
        {
            if (route == PeerRoute.None)
                throw new ArgumentException($"[Network] [Peers] No route for the guest {guestIndex}");

            _routes[guestIndex] = route;
        }
    }
}
