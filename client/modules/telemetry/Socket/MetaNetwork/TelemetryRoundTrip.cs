namespace Global.Telemetry
{
    // The time from a batch written to the meta socket to its answer: the network both ways, the frames of the writer and the
    // reader, and the work of the server on the batch. Written by TelemetrySocketTransport.
    public class TelemetryRoundTrip
    {
        private double _lastMs = -1d;

        public void Add(double milliseconds)
        {
            _lastMs = milliseconds;
        }

        // Below zero: no answer since the previous take.
        public double TakeMs()
        {
            var last = _lastMs;
            _lastMs = -1d;
            return last;
        }
    }
}
