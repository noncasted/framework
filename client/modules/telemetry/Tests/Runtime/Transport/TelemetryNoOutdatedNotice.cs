using System;
using Internal;

namespace Global.Telemetry.Tests.Transport
{
    // The host checks no protocol: a close of an outdated client here is a bug of the test.
    public class TelemetryNoOutdatedNotice : IClientOutdatedNotice
    {
        public bool IsShown => false;

        public void Show()
        {
            throw new InvalidOperationException("The test host closed the socket as of an outdated client");
        }
    }
}
