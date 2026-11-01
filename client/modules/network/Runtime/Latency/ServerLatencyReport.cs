using System.Text;

namespace Internal
{
    // The session is on the server: the ping and the loop to every other client through it.
    public static class ServerLatencyReport
    {
        public static void Append(StringBuilder builder, int localIndex, LatencyStats stats, int lostPings)
        {
            builder.Append($"Network (you: user {localIndex}), ms\n");
            builder.Append($"Ping to server: {stats.Ping.Format()}, lost {lostPings}\n");

            // Not one pong yet: a backend without the latency commands drops the probes as unknown messages.
            if (stats.Ping.Count == 0 && lostPings > 0)
                builder.Append("Server does not answer: is the backend deployed with the latency commands?\n");

            foreach (var (index, peer) in stats.Peers)
            {
                builder.Append($"User {index}: loop {peer.Loop.Format()}{LatencyReportFormat.Stale(peer)}\n");
                builder.Append($"  you <-> server {peer.OurLeg.Format()}\n");
                builder.Append($"  server <-> user {index} {peer.TheirLeg.Format()}, their ping {peer.TheirPing:0}\n");
            }
        }
    }
}
