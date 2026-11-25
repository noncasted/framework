namespace Internal.Tests
{
    internal sealed class BenchRootNetwork
    {
        public readonly BenchRootConfig Config;
        public readonly BenchRootLog Log;

        public BenchRootNetwork(BenchRootConfig config, BenchRootLog log)
        {
            Config = config;
            Log = log;
        }
    }
}
