namespace Internal.Tests
{
    internal sealed class BenchRootProfiler : IBenchBaseSetup
    {
        public readonly BenchRootTime Time;

        public BenchRootProfiler(BenchRootTime time)
        {
            Time = time;
        }
    }
}
