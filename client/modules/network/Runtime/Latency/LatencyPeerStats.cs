using Shared;
using UnityEngine;

namespace Internal
{
    // What the echoes of one other client say: the loop through the backend, split into the local leg and its leg.
    public class LatencyPeerStats
    {
        // 10 seconds of probes.
        public const int SeriesCapacity = 20;

        public readonly LatencySeries Loop = new(SeriesCapacity);
        public readonly LatencySeries OurLeg = new(SeriesCapacity);
        public readonly LatencySeries TheirLeg = new(SeriesCapacity);

        public float TheirPing { get; private set; }
        public double LastEchoAt { get; private set; }

        public void OnEcho(float loopMs, SharedSessionLatency.PeerEcho echo)
        {
            Loop.Add(loopMs);
            TheirLeg.Add(echo.ResponderLegMs);
            OurLeg.Add(loopMs - echo.ResponderLegMs);
            TheirPing = echo.ResponderPingMs;
            LastEchoAt = Time.realtimeSinceStartupAsDouble;
        }
    }
}
