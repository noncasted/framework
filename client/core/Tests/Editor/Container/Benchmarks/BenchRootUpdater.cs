namespace Internal.Tests
{
    internal sealed class BenchRootUpdater : IBenchSetup, IBenchDispose
    {
        public readonly BenchRootTime Time;
        public readonly BenchRootLog Log;

        public BenchRootUpdater(BenchRootTime time, BenchRootLog log)
        {
            Time = time;
            Log = log;
        }
    }
}
