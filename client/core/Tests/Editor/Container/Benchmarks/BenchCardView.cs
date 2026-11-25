namespace Internal.Tests
{
    internal sealed class BenchCardView : IBenchEntityComponent, IBenchSetup
    {
        public readonly BenchCardDefinition Definition;
        public readonly BenchRootPrefabs Prefabs;
        public readonly BenchMatchBoard Board;

        public BenchCardView(
            BenchCardDefinition definition,
            BenchRootPrefabs prefabs,
            BenchMatchBoard board)
        {
            Definition = definition;
            Prefabs = prefabs;
            Board = board;
        }
    }
}
