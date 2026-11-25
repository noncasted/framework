namespace Internal.Tests
{
    internal sealed class BenchMatchState : IBenchSetup
    {
        public readonly BenchRootConfig Config;
        public readonly BenchRootLog Log;

        public BenchMatchState(BenchRootConfig config, BenchRootLog log)
        {
            Config = config;
            Log = log;
        }
    }
}
