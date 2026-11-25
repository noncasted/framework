namespace Internal.Tests
{
    internal sealed class BenchMatchBoard : IBenchSetup, IBenchSceneService
    {
        public readonly BenchMatchState State;
        public readonly BenchRootPrefabs Prefabs;

        public BenchMatchBoard(BenchMatchState state, BenchRootPrefabs prefabs)
        {
            State = state;
            Prefabs = prefabs;
        }
    }
}
