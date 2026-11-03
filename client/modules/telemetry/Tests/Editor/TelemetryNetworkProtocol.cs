using Internal.Tests;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The real ConnectionWriter serializes a request when it is written, so a test that sends through it needs the union
    // over INetworkContext with the telemetry tags: the test union of the network (a game registers its own on load).
    [SetUpFixture]
    public class TelemetryNetworkProtocol
    {
        [OneTimeSetUp]
        public void Register()
        {
            TestNetworkProtocol.RegisterOnce();
        }
    }
}
