namespace Global.Telemetry
{
    // What waits for the next batch, in memory only. Everything is bounded: on overflow the oldest is lost and the game
    // does not notice. Open only while a session collects: otherwise it holds nothing and takes nothing.
    public class TelemetryBuffer
    {
        public TelemetryBuffer(ClientTelemetryOptions options, TelemetrySessionTrace trace)
        {
            Samples = new TelemetryRing<TelemetrySampleRecord>(options.BufferedSamples);
            _trace = trace;
        }

        private readonly TelemetrySessionTrace _trace;
        private bool _isOpen;

        public TelemetryRing<TelemetrySampleRecord> Samples { get; }
        public TelemetryRing<TelemetryMarkRecord> Marks { get; } = new(TelemetryLimits.MaxMarksPerBatch);
        public TelemetryRing<TelemetryErrorRecord> Errors { get; } = new(TelemetryLimits.MaxErrorsPerBatch);

        public void Open()
        {
            _isOpen = true;
        }

        public void AddSample(long time, double[] values, int count, bool isHidden)
        {
            if (_isOpen)
                Samples.Add().Set(time, values, count, isHidden);
        }

        public void AddMark(long time, string name, string detail = null, long? durationMs = null)
        {
            if (_isOpen == false)
                return;

            Marks.Add().Set(time, name, detail == null ? null : TelemetryLimits.Cut(detail, TelemetryLimits.MaxMarkDetailLength), durationMs);
            _trace.Marked(time, name);
        }

        public void AddError(long time, TelemetryErrorLevel level, string message, string stack, int repeat)
        {
            if (_isOpen)
                Errors.Add().Set(time, level, message, stack, repeat);
        }

        public void Close()
        {
            _isOpen = false;
            Samples.RemoveOldest(Samples.Count);
            Marks.RemoveOldest(Marks.Count);
            Errors.RemoveOldest(Errors.Count);
        }
    }
}
