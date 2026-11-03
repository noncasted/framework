using System;
using System.Diagnostics;
using Internal;

namespace Global.Telemetry
{
    public class ProfilerTelemetryStartupSource : ITelemetryStartupSource
    {
        public ProfilerTraceData ReadFinal()
        {
            return GameProfiler.Startup?.Final;
        }

        public ProfilerTraceData ReadSnapshot()
        {
            return GameProfiler.Startup?.Snapshot();
        }

        public double SinceStartMs()
        {
            return (Stopwatch.GetTimestamp() - GameProfiler.Startup.StartTimestamp) * 1000d / Stopwatch.Frequency;
        }

        public void ListenFinal(IReadOnlyLifetime lifetime, Action listener)
        {
            void OnSaved(ProfilerTraceData trace)
            {
                if (trace == ReadFinal())
                    listener();
            }

            ProfilerTraceStorage.Saved += OnSaved;
            lifetime.Listen(() => ProfilerTraceStorage.Saved -= OnSaved);
        }
    }
}
