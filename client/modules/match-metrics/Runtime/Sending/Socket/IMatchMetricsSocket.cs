using Internal;

namespace Global.MatchMetrics
{
    // The meta socket the data goes through: the game registers its meta backend as this, in the scope of
    // AddSocketMatchMetrics or above it. Read on every request: the socket runs the same connection again after a loss.
    public interface IMatchMetricsSocket
    {
        INetworkConnection Connection { get; }
    }
}
