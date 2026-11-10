using System;
using Internal;

namespace Global.MatchMetrics
{
    // The game data of matches. A game opens a stream per run of a match; what it writes goes to the server in batches
    // while the stream lives and once more at its end, kept and sent again over a new connection of the socket.
    public interface IMatchMetrics
    {
        // The stream lives with the lifetime: its end takes the last batch, the sending of what is left goes on after it.
        // matchId: the id of the session of the match. The buckets are seconds of the clock.
        IMatchMetricsStream Open(IReadOnlyLifetime lifetime, Guid matchId, IMatchMetricsClock clock);
    }
}
