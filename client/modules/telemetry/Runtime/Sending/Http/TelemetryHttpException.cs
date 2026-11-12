using System;

namespace Global.Telemetry
{
    // What a network does to a request of the telemetry: no answer, the timeout, a status that is not 2xx (0: no response).
    public class TelemetryHttpException : Exception
    {
        public TelemetryHttpException(int status, string message) : base(message)
        {
            Status = status;
        }

        public int Status { get; }
    }
}
