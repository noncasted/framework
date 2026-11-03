using System;
using Internal;

namespace Global.Telemetry.Tests
{
    // The first trace of the run in the hands of a test.
    public class FakeTelemetryStartupSource : ITelemetryStartupSource
    {
        private ProfilerTraceData _snapshot;
        private ProfilerTraceData _final;
        private Action _listener;

        public int Snapshots { get; private set; }

        public static ProfilerTraceData Trace(string name, params ProfilerSpanData[] spans)
        {
            var trace = new ProfilerTraceData { Name = name, DurationMs = 100d, Frames = 5 };

            trace.Spans.AddRange(spans);

            return trace;
        }

        public void Open(ProfilerTraceData snapshot)
        {
            _snapshot = snapshot;
        }

        public void Close(ProfilerTraceData final)
        {
            _final = final;
            _listener?.Invoke();
        }

        public ProfilerTraceData ReadFinal()
        {
            return _final;
        }

        public ProfilerTraceData ReadSnapshot()
        {
            Snapshots++;
            return _snapshot;
        }

        public void ListenFinal(IReadOnlyLifetime lifetime, Action listener)
        {
            _listener = listener;
            lifetime.Listen(() => _listener = null);
        }
    }
}
