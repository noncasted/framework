using System;

namespace Internal
{
    // The pieces of a report line shared by its kinds.
    public static class LatencyReportFormat
    {
        public static string Route(PeerRoute route)
        {
            return route switch
            {
                PeerRoute.Direct => "direct",
                PeerRoute.Tunnel => "tunnel via server",
                PeerRoute.None => "route not known yet",
                _ => throw new ArgumentOutOfRangeException(nameof(route), route, "[Network] [Latency] Unknown route")
            };
        }

        public static string Stale(LatencyPeerStats peer)
        {
            var silence = peer.Silence;
            return silence > LatencyProbes.Timeout ? $" NO ECHO {silence:0}s" : string.Empty;
        }
    }
}
