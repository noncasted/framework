namespace Internal.Tests
{
    internal sealed class BenchRootLog : IBenchBaseSetup
    {
        public readonly BenchRootTime Time;

        public BenchRootLog(BenchRootTime time)
        {
            Time = time;
        }
    }
}
