using System;
using Internal;

namespace Global.Telemetry.Tests.Transport
{
    // The first trace of the run in the hands of a test.
    public class TelemetryScriptedStartup : ITelemetryStartupSource
    {
        private ProfilerTraceData _snapshot;
        private ProfilerTraceData _final;
        private Action _listener;

        public static ProfilerTraceData Trace(string name, double durationMs, int frames, params ProfilerSpanData[] spans)
        {
            var trace = new ProfilerTraceData { Name = name, DurationMs = durationMs, Frames = frames };

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
            return _snapshot;
        }

        public double SinceStartMs()
        {
            return 0d;
        }

        public void ListenFinal(IReadOnlyLifetime lifetime, Action listener)
        {
            _listener = listener;
            lifetime.Listen(() => _listener = null);
        }
    }
}
