namespace Global.Telemetry.Tests
{
    // The trace of the session kept in memory: the tests of a real loop leave nothing on the disk of the editor.
    public class TelemetryMemoryTraceStore : ITelemetryTraceStore
    {
        private string _text;

        public static TelemetrySessionTrace Trace(TelemetryNameTable names)
        {
            return new TelemetrySessionTrace(new TelemetryMemoryTraceStore(), new TelemetryWallClock(), names);
        }

        public string Read()
        {
            return _text;
        }

        public void Write(string text)
        {
            _text = text;
        }

        public void Clear()
        {
            _text = null;
        }
    }
}
