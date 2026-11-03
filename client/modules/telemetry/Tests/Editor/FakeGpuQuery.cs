namespace Global.Telemetry.Tests
{
    // A query whose result is ready in the frame after its end.
    public class FakeGpuQuery : ITelemetryGpuQuery
    {
        private bool _isPending;

        public bool IsAvailable { get; set; }
        public double Result { get; set; } = 1d;
        public int Begins { get; private set; }
        public int Ends { get; private set; }
        public int Drops { get; private set; }

        public bool Begin()
        {
            Begins++;
            return true;
        }

        public void End()
        {
            Ends++;
            _isPending = true;
        }

        public double TakeMs()
        {
            if (_isPending == false)
                return TelemetryGpuQueryResults.Lost;

            _isPending = false;
            return Result;
        }

        public void Drop()
        {
            Drops++;
        }
    }
}
