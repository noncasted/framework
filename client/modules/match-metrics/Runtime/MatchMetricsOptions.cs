using System;

namespace Global.MatchMetrics
{
    // Built by the game, or the defaults.
    public class MatchMetricsOptions
    {
        public MatchMetricsOptions(float sendIntervalSeconds = 30f, float tickSeconds = 1f, int bufferedBatches = 20)
        {
            if (sendIntervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(sendIntervalSeconds), sendIntervalSeconds, "[MatchMetrics] Must be above zero");

            if (tickSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "[MatchMetrics] Must be above zero");

            if (bufferedBatches < 2)
                throw new ArgumentOutOfRangeException(nameof(bufferedBatches), bufferedBatches, "[MatchMetrics] At least 2: the one in flight and the next");

            SendIntervalMs = sendIntervalSeconds * 1000d;
            Tick = TimeSpan.FromSeconds(tickSeconds);
            BufferedBatches = bufferedBatches;
        }

        // A batch of a stream every this much real time, and one at its end; a request without an answer on a live link is
        // tried again after it.
        public double SendIntervalMs { get; }

        // The real time between the checks of the streams: the stopped clocks, the batches due, a socket back.
        public TimeSpan Tick { get; }

        // The batches kept unconfirmed per stream: 20 x 30 s - ten minutes without the server.
        public int BufferedBatches { get; }
    }
}
