using System.Collections.Generic;
using System.Text;

namespace Internal
{
    // The session is in this process: the ping and the own leg of a probe are the loopback, the leg of a guest is the
    // connection to it.
    public static class HostLatencyReport
    {
        public static void Append(
            StringBuilder builder,
            int localIndex,
            LatencyStats stats,
            IReadOnlyDictionary<int, PeerRoute> routes)
        {
            builder.Append($"Network (you: user {localIndex}, P2P host), ms\n");

            foreach (var (index, peer) in stats.Peers)
            {
                var route = LatencyReportFormat.Route(routes[index]);
                builder.Append($"User {index}: you <-> user {index} {peer.TheirLeg.Format()}, {route}{LatencyReportFormat.Stale(peer)}\n");
            }
        }
    }
}
