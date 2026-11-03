using System;
using System.Collections.Generic;

namespace Global.Telemetry
{
    public class TelemetrySessionStart
    {
        public TelemetrySessionStart(IReadOnlyList<string> tags, TelemetryDevice device, Guid startupId)
        {
            Tags = tags;
            Device = device;
            StartupId = startupId;
        }

        public IReadOnlyList<string> Tags { get; }
        public TelemetryDevice Device { get; }

        // The startup record of this client on this connection. Guid.Empty: none.
        public Guid StartupId { get; }
    }
}
