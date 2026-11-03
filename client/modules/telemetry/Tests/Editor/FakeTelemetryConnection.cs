using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Telemetry.Tests
{
    public class FakeTelemetryConnection : INetworkConnection
    {
        public FakeTelemetryConnection(IConnectionWriter writer)
        {
            Writer = writer;
        }

        public IConnectionReader Reader => throw new NotSupportedException();
        public IConnectionWriter Writer { get; }
        public IViewableDelegate<SocketCloseInfo> Closed { get; } = new ViewableDelegate<SocketCloseInfo>();
        public NetworkTraffic Traffic { get; } = new NetworkTraffic();

        public UniTask Run(IReadOnlyLifetime lifetime, string url) => throw new NotSupportedException();
        public void Drop(string reason) => throw new NotSupportedException();
    }
}
