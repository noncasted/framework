namespace Internal.Tests
{
    internal sealed class BenchMatchLoop : IBenchSetup, IBenchLoaded
    {
        public readonly BenchMatchState State;
        public readonly BenchMatchBoard Board;
        public readonly BenchRootUpdater Updater;

        public BenchMatchLoop(
            BenchMatchState state,
            BenchMatchBoard board,
            BenchRootUpdater updater)
        {
            State = state;
            Board = board;
            Updater = updater;
        }
    }
}
