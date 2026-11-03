using System.Collections.Generic;

namespace Global.Telemetry.Tests
{
    // The page side of TelemetryGlCallErrors: the counters and the first errors wait until taken.
    public class FakeTelemetryGlCallErrors : ITelemetryGlCallErrors
    {
        private readonly List<TelemetryGlCallError> _details = new();
        private int _recorded;
        private int _checked;

        public bool IsChecking { get; private set; }

        public void Check()
        {
            IsChecking = true;
        }

        public void Record(int code, string call, double pageMs)
        {
            _recorded++;
            _details.Add(new TelemetryGlCallError(false, code, call, string.Empty, -1, pageMs));
        }

        public void Fail(int code, string call, string arguments, int frame, double pageMs)
        {
            _checked++;
            _details.Add(new TelemetryGlCallError(true, code, call, arguments, frame, pageMs));
        }

        public void RecordAgain()
        {
            _recorded++;
        }

        public TelemetryGlCallErrorCounts Take(List<TelemetryGlCallError> details)
        {
            details.AddRange(_details);
            _details.Clear();

            var counts = new TelemetryGlCallErrorCounts(_recorded, _checked, IsChecking);
            _recorded = 0;
            _checked = 0;

            return counts;
        }
    }
}
