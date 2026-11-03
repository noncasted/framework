using Unity.Profiling;

namespace Global.Telemetry
{
    // The frames a recorder kept since the previous take: its capacity covers a sample, the oldest go when it is longer.
    public class ProfilerTelemetryFrameSeries : ITelemetryFrameSeries
    {
        public ProfilerTelemetryFrameSeries(ProfilerRecorder recorder)
        {
            _recorder = recorder;
        }

        private ProfilerRecorder _recorder;

        public int Take(out double sum, out double max)
        {
            var count = _recorder.Count;

            sum = 0d;
            max = 0d;

            for (var i = 0; i < count; i++)
            {
                double value = _recorder.GetSample(i).Value;

                sum += value;

                if (value > max)
                    max = value;
            }

            _recorder.Reset();

            return count;
        }

        public void Close()
        {
            _recorder.Dispose();
        }
    }
}
