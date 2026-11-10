namespace Global.MatchMetrics.Tests
{
    // The clock of the run and the real time of a test, moved by hand.
    public class ManualMatchClock : IMatchMetricsClock, IMatchMetricsTicks
    {
        public float Seconds { get; private set; }
        public double NowMs { get; private set; } = 5000d;

        // The game runs: both clocks go.
        public void Run(float seconds)
        {
            Seconds += seconds;
            NowMs += seconds * 1000d;
        }

        // A pause: only the real time goes.
        public void Wait(float seconds)
        {
            NowMs += seconds * 1000d;
        }
    }
}
