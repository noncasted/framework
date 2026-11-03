using System;
using System.Collections.Generic;

namespace Global.Telemetry
{
    // The session before this one that did not close on this device, as its trace kept it. Times of that session; SinceMs from its
    // last record to the start that reports it. -1: unknown.
    public class TelemetryPreviousSession
    {
        public TelemetryPreviousSession(Guid sessionId, IReadOnlyList<string> tags, long lastSampleTime, long sinceMs, IReadOnlyList<TelemetryBatchMark> marks)
        {
            SessionId = sessionId;
            Tags = tags;
            LastSampleTime = lastSampleTime;
            SinceMs = sinceMs;
            Marks = marks;
        }

        public Guid SessionId { get; }
        public IReadOnlyList<string> Tags { get; }
        public long LastSampleTime { get; }
        public long SinceMs { get; }
        public IReadOnlyList<TelemetryBatchMark> Marks { get; }
    }
}
