namespace Global.FrameRate
{
    // The decision of the automatic frame rate, fed with the work of every frame (AutoFrameRateOptions). The work is averaged
    // over windows; the limit changes only after the average of every window of a whole hold is past the threshold, and the
    // count starts anew after a change, after a window that is not past it and after a gap in the frames.
    public class AutoFrameRateRule
    {
        public AutoFrameRateRule(AutoFrameRateOptions options, bool isLow)
        {
            options.Validate();

            _options = options;
            IsLow = isLow;
        }

        private readonly AutoFrameRateOptions _options;

        private bool _hasFrame;
        private double _lastFrame;
        private double _windowStart;
        private double _windowWork;
        private int _windowFrames;
        private double _held;

        public bool IsLow { get; private set; }
        public int Target => IsLow == true ? _options.Low : _options.High;

        // The time of the frame in seconds and its work in milliseconds; true when the limit has changed.
        public bool Feed(double now, double workMs)
        {
            if (_hasFrame == false || now - _lastFrame > _options.WindowSeconds)
            {
                // The first frame after a gap is the one that waited (a load, a hidden tab): it is left out.
                _hasFrame = true;
                _lastFrame = now;
                Restart(now);
                return false;
            }

            _lastFrame = now;
            _windowWork += workMs;
            _windowFrames++;

            var elapsed = now - _windowStart;

            if (elapsed < _options.WindowSeconds)
                return false;

            var average = _windowWork / _windowFrames;
            StartWindow(now);

            if (IsPast(average) == false)
            {
                _held = 0d;
                return false;
            }

            _held += elapsed;

            if (_held < (IsLow == true ? _options.RiseSeconds : _options.DropSeconds))
                return false;

            IsLow = IsLow == false;
            _held = 0d;
            return true;
        }

        private bool IsPast(double average)
        {
            return IsLow == true ? average < _options.RiseWorkMs : average > _options.DropWorkMs;
        }

        private void Restart(double now)
        {
            _held = 0d;
            StartWindow(now);
        }

        private void StartWindow(double now)
        {
            _windowStart = now;
            _windowWork = 0d;
            _windowFrames = 0;
        }
    }
}
