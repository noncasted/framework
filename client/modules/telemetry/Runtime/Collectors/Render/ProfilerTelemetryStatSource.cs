using System.Collections.Generic;
using Internal;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Global.Telemetry
{
    // Asks the profiler for its list first: a recorder of a counter that does not exist may still look valid.
    public class ProfilerTelemetryStatSource : ITelemetryStatSource
    {
        private HashSet<(string Category, string Name)> _available;

        public bool TryOpen(IReadOnlyLifetime lifetime, string category, string name, out ITelemetryStat stat)
        {
            stat = null;
            _available ??= ReadAvailable();

            if (_available.Contains((category, name)) == false)
                return false;

            var recorder = ProfilerRecorder.StartNew(new ProfilerCategory(category), name, 1);

            if (recorder.Valid == false)
                return false;

            var opened = new ProfilerTelemetryStat(recorder);
            lifetime.Listen(opened.Close);
            stat = opened;

            return true;
        }

        private static HashSet<(string, string)> ReadAvailable()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);

            var available = new HashSet<(string, string)>();

            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                available.Add((description.Category.Name, description.Name));
            }

            return available;
        }
    }
}
