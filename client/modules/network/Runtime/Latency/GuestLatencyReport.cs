using System.Collections.Generic;
using System.Text;

namespace Internal
{
    // The session is on the host: the own leg of a probe is the connection to the host, the leg of the host is its
    // loopback, the leg of another guest is the connection of the host to it.
    public static class GuestLatencyReport
    {
        public static void Append(
            StringBuilder builder,
            int localIndex,
            int hostIndex,
            LatencyStats stats,
            int lostPings,
            IReadOnlyDictionary<int, PeerRoute> routes)
        {
            var route = LatencyReportFormat.Route(routes[localIndex]);

            builder.Append($"Network (you: user {localIndex}, P2P guest of user {hostIndex}, {route}), ms\n");
            builder.Append($"Ping to host: {stats.Ping.Format()}, lost {lostPings}\n");

            foreach (var (index, peer) in stats.Peers)
            {
                if (index == hostIndex)
                {
                    builder.Append($"User {index} (host): you <-> user {index} {peer.OurLeg.Format()}{LatencyReportFormat.Stale(peer)}\n");
                    continue;
                }

                builder.Append($"User {index}: loop {peer.Loop.Format()}{LatencyReportFormat.Stale(peer)}\n");
                builder.Append($"  you <-> host {peer.OurLeg.Format()}\n");
                builder.Append($"  host <-> user {index} {peer.TheirLeg.Format()}\n");
            }
        }
    }
}
