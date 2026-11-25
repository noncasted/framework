namespace Internal.Tests
{
    internal sealed class BenchRootPublisher : IBenchSetup
    {
        public readonly BenchRootLog Log;

        public BenchRootPublisher(BenchRootLog log)
        {
            Log = log;
        }
    }
}
