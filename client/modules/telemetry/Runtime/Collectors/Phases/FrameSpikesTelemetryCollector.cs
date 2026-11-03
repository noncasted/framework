using Internal;

namespace Global.Telemetry
{
    // A frame of 250 ms or more gives the mark frame.spike and the mark of its longest part: frame.spike.<phase>, or
    // frame.spike.browser when the time between the frames was the longest (the browser did not give the frame, or the GPU held
    // it). A session marks the first MaxMarked of them; frame.spikes counts all, per sample. The frame after a hidden tab is no spike.
    public class FrameSpikesTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector, IFramePhaseListener
    {
        public FrameSpikesTelemetryCollector(TelemetryBuffer buffer, ITelemetryClock clock, ITelemetryPage page)
        {
            _buffer = buffer;
            _clock = clock;
            _page = page;

            for (var i = 0; i < FramePhases.Count; i++)
                _phaseMarks[i] = SpikeMark + "." + FramePhases.Names[i];
        }

        public const double ThresholdMs = 250d;
        public const int MaxMarked = 64;
        public const string SpikeMark = "frame.spike";
        public const string BrowserMark = SpikeMark + ".browser";

        private readonly TelemetryBuffer _buffer;
        private readonly ITelemetryClock _clock;
        private readonly ITelemetryPage _page;
        private readonly string[] _phaseMarks = new string[FramePhases.Count];

        private bool _wasHidden;
        private bool _isRunning;
        private int _marked;
        private int _spikes;

        public void Start(IReadOnlyLifetime running)
        {
            _marked = 0;
            _spikes = 0;
            _wasHidden = false;
            _isRunning = true;
            _page.BecameHidden.Advise(running, OnHidden);
            running.Listen(OnStopped);
        }

        public void OnFrameStarted()
        {
        }

        public void OnFrameEnded(FramePhaseFrame frame)
        {
            if (_wasHidden || _page.IsHidden)
            {
                _wasHidden = false;
                return;
            }

            if (frame.IntervalMs < ThresholdMs)
                return;

            _spikes++;

            if (_marked == MaxMarked)
                return;

            _marked++;
            var time = _clock.ElapsedMilliseconds;
            _buffer.AddMark(time, SpikeMark);
            _buffer.AddMark(time, LongestPart(frame));
        }

        public void Collect(ITelemetrySample sample)
        {
            if (_isRunning == false)
                return;

            sample.Gauge("frame.spikes", _spikes, TelemetryUnits.Count);
            _spikes = 0;
        }

        private string LongestPart(FramePhaseFrame frame)
        {
            var longest = BrowserMark;
            var longestMs = frame.OutsideMs();

            for (var i = 0; i < FramePhases.Count; i++)
            {
                if (frame.SpentMs(i) <= longestMs)
                    continue;

                longestMs = frame.SpentMs(i);
                longest = _phaseMarks[i];
            }

            return longest;
        }

        private void OnHidden()
        {
            _wasHidden = true;
        }

        private void OnStopped()
        {
            _isRunning = false;
        }
    }
}
