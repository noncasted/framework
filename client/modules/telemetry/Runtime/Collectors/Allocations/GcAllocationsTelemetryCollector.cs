using Internal;

namespace Global.Telemetry
{
    // gc.allocated: the bytes the managed heap grew by, frame by frame, over the sample. A frame with a collection adds nothing,
    // so a sample with gc.collections above zero reads low by what was allocated in that frame. The engine has no counter of the
    // allocations in a release (the recorder "GC Allocated In Frame" is of the development builds), nor of the pauses of the GC.
    public class GcAllocationsTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector, IFramePhaseListener
    {
        public GcAllocationsTelemetryCollector(ITelemetryMemoryReadings readings)
        {
            _readings = readings;
        }

        private readonly ITelemetryMemoryReadings _readings;

        private long _last;
        private long _allocated;
        private bool _isRunning;

        public void Start(IReadOnlyLifetime running)
        {
            _last = _readings.MonoUsed;
            _allocated = 0;
            _isRunning = _last > 0;
            running.Listen(OnStopped);
        }

        public void OnFrameStarted()
        {
        }

        public void OnFrameEnded(FramePhaseFrame frame)
        {
            if (_isRunning == false)
                return;

            var used = _readings.MonoUsed;

            if (used > _last)
                _allocated += used - _last;

            _last = used;
        }

        public void Collect(ITelemetrySample sample)
        {
            if (_isRunning == false)
                return;

            sample.Gauge("gc.allocated", _allocated, TelemetryUnits.Bytes);
            _allocated = 0;
        }

        private void OnStopped()
        {
            _isRunning = false;
        }
    }
}
