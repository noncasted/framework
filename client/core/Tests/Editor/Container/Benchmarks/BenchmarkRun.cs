namespace Internal.Tests
{
    internal readonly struct BenchmarkRun
    {
        public readonly long Ticks;
        public readonly long AllocatedBytes;

        public BenchmarkRun(long ticks, long allocatedBytes)
        {
            Ticks = ticks;
            AllocatedBytes = allocatedBytes;
        }
    }
}
