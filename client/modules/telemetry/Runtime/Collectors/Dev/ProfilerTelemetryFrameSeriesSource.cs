using System.Collections.Generic;
using Internal;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Global.Telemetry
{
    // ProfilerRecorder by the list of the profiler: the category of a marker is found by its name, a marker absent from the list (a
    // ProfilerMarker of the game not created yet) is asked for in the category of the scripts, where a ProfilerMarker goes by default.
    public class ProfilerTelemetryFrameSeriesSource : ITelemetryFrameSeriesSource
    {
        // The frames of one sample with room: 1 s at 240 fps.
        private const int Capacity = 256;

        private Dictionary<string, string> _categories;
        private HashSet<(string Category, string Name)> _available;

        public bool TryOpenCounter(IReadOnlyLifetime lifetime, string category, string name, out ITelemetryFrameSeries series)
        {
            series = null;
            ReadAvailable();

            if (_available.Contains((category, name)) == false)
                return false;

            var recorder = ProfilerRecorder.StartNew(new ProfilerCategory(category), name, Capacity);

            if (recorder.Valid == false)
                return false;

            series = Opened(lifetime, recorder);
            return true;
        }

        public ITelemetryFrameSeries OpenMarker(IReadOnlyLifetime lifetime, string name)
        {
            ReadAvailable();

            var category = _categories.TryGetValue(name, out var known) ? new ProfilerCategory(known) : ProfilerCategory.Scripts;

            return Opened(lifetime, ProfilerRecorder.StartNew(category, name, Capacity));
        }

        private static ITelemetryFrameSeries Opened(IReadOnlyLifetime lifetime, ProfilerRecorder recorder)
        {
            var series = new ProfilerTelemetryFrameSeries(recorder);
            lifetime.Listen(series.Close);
            return series;
        }

        private void ReadAvailable()
        {
            if (_available != null)
                return;

            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);

            _available = new HashSet<(string, string)>();
            _categories = new Dictionary<string, string>();

            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);

                _available.Add((description.Category.Name, description.Name));
                _categories.TryAdd(description.Name, description.Category.Name);
            }
        }
    }
}
