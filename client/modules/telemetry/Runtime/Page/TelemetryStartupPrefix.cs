using System;
using System.Collections.Generic;

namespace Global.Telemetry
{
    // The spans of the page that go ahead of the trace of the build, and the start of the build on the time line of the page: the
    // spans of the build move by it. None: the build is the first code (outside the web).
    public readonly struct TelemetryStartupPrefix
    {
        public TelemetryStartupPrefix(IReadOnlyList<TelemetryStartupSpan> spans, double offsetMs)
        {
            Spans = spans;
            OffsetMs = offsetMs;
        }

        public static TelemetryStartupPrefix None => new(Array.Empty<TelemetryStartupSpan>(), 0d);

        public IReadOnlyList<TelemetryStartupSpan> Spans { get; }
        public double OffsetMs { get; }
    }
}
