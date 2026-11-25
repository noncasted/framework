namespace Internal.Tests
{
    internal sealed class BenchLarge07 : IBenchLoaded
    {
        public readonly BenchRootPrefabs Parent;
        public readonly BenchLarge06 Previous;

        public BenchLarge07(BenchRootPrefabs parent, BenchLarge06 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }
}
