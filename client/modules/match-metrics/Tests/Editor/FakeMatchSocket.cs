using Internal;

namespace Global.MatchMetrics.Tests
{
    public class FakeMatchSocket : IMatchMetricsSocket
    {
        public FakeMatchSocket(IConnectionWriter writer)
        {
            Connection = new FakeMatchConnection(writer);
        }

        public INetworkConnection Connection { get; }
    }
}
