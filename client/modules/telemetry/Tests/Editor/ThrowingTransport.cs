using System;
using Cysharp.Threading.Tasks;

namespace Global.Telemetry.Tests
{
    // A bug behind the interface, as opposed to a failure the transport reports.
    public class ThrowingTransport : ITelemetryTransport
    {
        public int Calls { get; private set; }

        public UniTask<TelemetrySessionAnswer> Start(TelemetrySessionStart start)
        {
            Calls++;
            throw new InvalidOperationException("bug in the transport");
        }

        public UniTask<TelemetrySendOutcome> Send(Guid sessionId, TelemetryBatch batch)
        {
            Calls++;
            throw new InvalidOperationException("bug in the transport");
        }

        public object Link { get; } = new object();

        public UniTask<TelemetryStartupAnswer> SendStartup(TelemetryStartupTrace trace)
        {
            Calls++;
            throw new InvalidOperationException("bug in the transport");
        }

        public UniTask End(Guid sessionId)
        {
            Calls++;
            throw new InvalidOperationException("bug in the transport");
        }
    }
}
