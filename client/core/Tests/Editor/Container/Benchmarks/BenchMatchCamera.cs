namespace Internal.Tests
{
    internal sealed class BenchMatchCamera : IBenchSceneService
    {
        public readonly BenchMatchBoard Board;
        public readonly BenchRootPrefabs Prefabs;

        public BenchMatchCamera(BenchMatchBoard board, BenchRootPrefabs prefabs)
        {
            Board = board;
            Prefabs = prefabs;
        }
    }
}
