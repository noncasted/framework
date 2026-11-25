namespace Internal.Tests
{
    internal sealed class BenchMatchClock : IBenchBaseSetup
    {
        public readonly BenchRootTime Time;

        public BenchMatchClock(BenchRootTime time)
        {
            Time = time;
        }
    }
}
