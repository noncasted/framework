namespace Internal.Tests
{
    internal sealed class BenchMatchSync : IBenchSetupAsync
    {
        public readonly BenchMatchState State;
        public readonly BenchRootNetwork Network;

        public BenchMatchSync(BenchMatchState state, BenchRootNetwork network)
        {
            State = state;
            Network = network;
        }
    }
}
