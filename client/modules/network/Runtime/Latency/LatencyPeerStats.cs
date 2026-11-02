using Shared;

namespace Internal
{
    // What the echoes of one other client say: the loop through the backend, split into the local leg and its leg.
    public class LatencyPeerStats
    {
        public LatencyPeerStats(ILatencyClock clock)
        {
            _clock = clock;
        }

        // 10 seconds of probes.
        public const int SeriesCapacity = 20;

        private readonly ILatencyClock _clock;

        private double _lastEchoAt;

        public readonly LatencySeries Loop = new(SeriesCapacity);
        public readonly LatencySeries OurLeg = new(SeriesCapacity);
        public readonly LatencySeries TheirLeg = new(SeriesCapacity);

        public float TheirPing { get; private set; }
        public double Silence => _clock.Now - _lastEchoAt;

        public void OnEcho(float loopMs, SharedSessionLatency.PeerEcho echo)
        {
            Loop.Add(loopMs);
            TheirLeg.Add(echo.ResponderLegMs);
            OurLeg.Add(loopMs - echo.ResponderLegMs);
            TheirPing = echo.ResponderPingMs;
            _lastEchoAt = _clock.Now;
        }
    }
}
