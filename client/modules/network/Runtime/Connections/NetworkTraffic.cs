namespace Internal
{
    // Counters of one connection for the network overlay: written by the reader and the writer, read and reset by
    // NetworkLatencyMonitor.
    public class NetworkTraffic
    {
        public long SentMessages { get; private set; }
        public long SentBytes { get; private set; }
        public long ReceivedMessages { get; private set; }
        public long ReceivedBytes { get; private set; }

        // The largest number of messages the writer sent in one frame since the last TakePeakBatch.
        private int _peakBatch;

        public void OnSent(int bytes)
        {
            SentMessages++;
            SentBytes += bytes;
        }

        public void OnReceived(int bytes)
        {
            ReceivedMessages++;
            ReceivedBytes += bytes;
        }

        public void OnBatch(int count)
        {
            if (count > _peakBatch)
                _peakBatch = count;
        }

        public int TakePeakBatch()
        {
            var peak = _peakBatch;
            _peakBatch = 0;
            return peak;
        }
    }
}
