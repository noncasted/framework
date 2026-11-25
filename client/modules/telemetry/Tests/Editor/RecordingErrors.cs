using System.Collections.Generic;

namespace Global.Telemetry.Tests
{
    public class RecordingErrors : ITelemetryErrors
    {
        public List<ErrorEntry> Entries { get; } = new List<ErrorEntry>();

        public void Add(TelemetryErrorLevel level, string category, string message, string stack, int repeat)
        {
            Entries.Add(new ErrorEntry(level, category, message, stack, repeat));
        }
    }
}
