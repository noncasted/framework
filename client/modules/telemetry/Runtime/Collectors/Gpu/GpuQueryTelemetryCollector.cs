using Internal;

namespace Global.Telemetry
{
    // frame.gpu.query: the time of the GPU over the whole frame, from its first phase to its last, measured on one frame in
    // IntervalFrames and averaged over the sample. Without gl.finish: the result is read in a later frame. Where the browser has
    // no such timer there is no metric.
    public class GpuQueryTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector, IFramePhaseListener
    {
        public GpuQueryTelemetryCollector(ITelemetryGpuQuery query)
        {
            _query = query;
        }

        public const int IntervalFrames = 30;

        private readonly ITelemetryGpuQuery _query;
        private readonly FrameTimingAverage _average = new();

        private bool _isRunning;
        private bool _isOpen;
        private bool _isPending;
        private int _framesLeft;

        public void Start(IReadOnlyLifetime running)
        {
            _average.Clear();
            _isOpen = false;
            _isPending = false;
            _framesLeft = 0;
            _isRunning = _query.IsAvailable;
            running.Listen(OnStopped);
        }

        public void OnFrameStarted()
        {
            if (_isRunning == false)
                return;

            if (_isPending)
                Poll();

            if (_isPending || --_framesLeft > 0)
                return;

            _isOpen = _query.Begin();
            _framesLeft = IntervalFrames;
        }

        public void OnFrameEnded(FramePhaseFrame frame)
        {
            if (_isOpen == false)
                return;

            _query.End();
            _isOpen = false;
            _isPending = true;
        }

        public void Collect(ITelemetrySample sample)
        {
            if (_average.TryTake(out var gpu))
                sample.Gauge("frame.gpu.query", gpu, TelemetryUnits.Milliseconds);
        }

        private void Poll()
        {
            var ms = _query.TakeMs();

            if (ms == TelemetryGpuQueryResults.NotReady)
                return;

            _isPending = false;
            _average.Add(ms);
        }

        private void OnStopped()
        {
            _isRunning = false;

            if (_isOpen || _isPending)
                _query.Drop();

            _isOpen = false;
            _isPending = false;
        }
    }
}
