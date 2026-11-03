namespace Global.Telemetry
{
    public class TelemetryErrors : ITelemetryErrors
    {
        public TelemetryErrors(TelemetryBuffer buffer, ITelemetryClock clock)
        {
            _buffer = buffer;
            _clock = clock;
        }

        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryClock _clock;

        public void Add(TelemetryErrorLevel level, string message, string stack, int repeat)
        {
            // The server rejects a whole batch for a longer text.
            _buffer.AddError(
                _clock.ElapsedMilliseconds,
                level,
                TelemetryLimits.Cut(message, TelemetryLimits.MaxMessageLength),
                stack == null ? null : TelemetryLimits.Cut(stack, TelemetryLimits.MaxStackLength),
                repeat);
        }
    }
}
