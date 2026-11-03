using System.Collections.Generic;

namespace Global.Telemetry
{
    // A part of a session on its way: a copy, the records of the buffer are reused. Names are the new names of the session,
    // each takes the next slot; a sample has its values by the slots. Times are milliseconds since the start of the session.
    public class TelemetryBatch
    {
        public TelemetryBatch(
            IReadOnlyList<TelemetryBatchName> names,
            IReadOnlyList<TelemetryBatchSample> samples,
            IReadOnlyList<TelemetryBatchMark> marks,
            IReadOnlyList<TelemetryBatchError> errors)
        {
            Names = names;
            Samples = samples;
            Marks = marks;
            Errors = errors;
        }

        public IReadOnlyList<TelemetryBatchName> Names { get; }
        public IReadOnlyList<TelemetryBatchSample> Samples { get; }
        public IReadOnlyList<TelemetryBatchMark> Marks { get; }
        public IReadOnlyList<TelemetryBatchError> Errors { get; }
    }
}
