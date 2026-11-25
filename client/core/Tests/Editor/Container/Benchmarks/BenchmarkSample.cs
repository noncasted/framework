namespace Internal.Tests
{
    internal readonly struct BenchmarkSample
    {
        public readonly string Name;
        public readonly int Runs;
        public readonly long Ticks;
        public readonly double Milliseconds;
        public readonly double MinMilliseconds;
        public readonly double MaxMilliseconds;
        public readonly long AllocatedBytes;
        public readonly long MaxAllocatedBytes;

        public BenchmarkSample(
            string name,
            int runs,
            long ticks,
            long minTicks,
            long maxTicks,
            long allocatedBytes,
            long maxAllocatedBytes)
        {
            Name = name;
            Runs = runs;
            Ticks = ticks;
            Milliseconds = ToMilliseconds(ticks);
            MinMilliseconds = ToMilliseconds(minTicks);
            MaxMilliseconds = ToMilliseconds(maxTicks);
            AllocatedBytes = allocatedBytes;
            MaxAllocatedBytes = maxAllocatedBytes;
        }

        private static double ToMilliseconds(long ticks)
        {
            return ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }
    }
}
