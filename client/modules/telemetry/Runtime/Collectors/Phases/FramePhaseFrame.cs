namespace Global.Telemetry
{
    // One whole frame of FramePhaseClock, in milliseconds.
    public class FramePhaseFrame
    {
        private readonly double[] _spent = new double[FramePhases.Count];

        // From the end of the previous frame to the end of this one: zero for the first frame of a session.
        public double IntervalMs { get; private set; }
        public int FixedSteps { get; private set; }

        public double SpentMs(int phase)
        {
            return _spent[phase];
        }

        // The interval less the phases: the short systems outside them and the time the browser kept the frame.
        public double OutsideMs()
        {
            var outside = IntervalMs;

            for (var i = 0; i < _spent.Length; i++)
                outside -= _spent[i];

            return outside;
        }

        public void Clear()
        {
            System.Array.Clear(_spent, 0, _spent.Length);
            IntervalMs = 0d;
            FixedSteps = 0;
        }

        public void AddSpent(int phase, double milliseconds)
        {
            _spent[phase] += milliseconds;
        }

        public void AddFixedStep()
        {
            FixedSteps++;
        }

        public void SetInterval(double milliseconds)
        {
            IntervalMs = milliseconds;
        }
    }
}
