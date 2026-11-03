using Internal;

namespace Global.Telemetry.Tests
{
    public class FakeTelemetrySocket : ITelemetrySocket
    {
        public FakeTelemetrySocket(IConnectionWriter writer)
        {
            Connect(writer);
        }

        public INetworkConnection Connection { get; private set; }

        // The socket opened again, or a new one.
        public void Connect(IConnectionWriter writer)
        {
            Connection = new FakeTelemetryConnection(writer);
        }
    }
}
