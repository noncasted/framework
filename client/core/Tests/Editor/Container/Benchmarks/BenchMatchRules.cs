namespace Internal.Tests
{
    internal sealed class BenchMatchRules
    {
        public readonly BenchMatchState State;
        public readonly BenchRootConfig Config;

        public BenchMatchRules(BenchMatchState state, BenchRootConfig config)
        {
            State = state;
            Config = config;
        }
    }
}
