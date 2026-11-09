using System;

namespace Internal.Tests
{
    // An IProgress that reports at once, on the thread of the report (Progress<T> posts to the synchronization context).
    public sealed class SyncProgress : IProgress<long>
    {
        public SyncProgress(Action<long> report)
        {
            _report = report;
        }

        private readonly Action<long> _report;

        public void Report(long value)
        {
            _report(value);
        }
    }
}
