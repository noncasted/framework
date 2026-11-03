using System;
using System.Collections.Generic;

namespace Global.Telemetry
{
    // The trace of the start of the client, ready for the wire: only the times from its start, no clock of the device.
    public class TelemetryStartupTrace
    {
        public TelemetryStartupTrace(
            Guid startupId,
            TelemetryDevice device,
            string name,
            bool isFinal,
            double durationMs,
            int frames,
            IReadOnlyList<TelemetryStartupSpan> spans)
        {
            StartupId = startupId;
            Device = device;
            Name = name;
            IsFinal = isFinal;
            DurationMs = durationMs;
            Frames = frames;
            Spans = spans;
        }

        // Guid.Empty asks the server for a new record.
        public Guid StartupId { get; }
        public TelemetryDevice Device { get; }
        public string Name { get; }
        public bool IsFinal { get; }
        public double DurationMs { get; }
        public int Frames { get; }
        public IReadOnlyList<TelemetryStartupSpan> Spans { get; }
    }
}
