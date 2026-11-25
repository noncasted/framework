namespace Internal.Tests
{
    internal sealed class BenchMatchPlayers : IBenchLoaded
    {
        public readonly BenchMatchState State;
        public readonly BenchRootNetwork Network;

        public BenchMatchPlayers(BenchMatchState state, BenchRootNetwork network)
        {
            State = state;
            Network = network;
        }
    }
}
