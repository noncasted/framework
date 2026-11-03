using System;
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
