using Unity.Profiling;

namespace Global.Telemetry
{
    public class ProfilerTelemetryStat : ITelemetryStat
    {
        public ProfilerTelemetryStat(ProfilerRecorder recorder)
        {
            _recorder = recorder;
        }

        private ProfilerRecorder _recorder;

        public bool IsValid => _recorder.Valid;
        public long LastValue => _recorder.LastValue;

        public void Close()
        {
            _recorder.Dispose();
        }
    }
}
