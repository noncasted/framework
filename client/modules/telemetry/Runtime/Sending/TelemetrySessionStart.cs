using System;
using System.Collections.Generic;

namespace Global.Telemetry
{
    public class TelemetrySessionStart
    {
        public TelemetrySessionStart(IReadOnlyList<string> tags, TelemetryDevice device, Guid startupId, TelemetryPreviousSession previous = null)
        {
            Tags = tags;
            Device = device;
            StartupId = startupId;
            Previous = previous;
        }

        public IReadOnlyList<string> Tags { get; }
        public TelemetryDevice Device { get; }

        // The startup record of this client on this connection. Guid.Empty: none.
        public Guid StartupId { get; }

        // The session before this one that did not close on this device. Null: none.
        public TelemetryPreviousSession Previous { get; }
    }
}
