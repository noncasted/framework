namespace Internal.Tests
{
    internal sealed class BenchLarge05
    {
        public readonly BenchRootTime Parent;
        public readonly BenchLarge04 Previous;

        public BenchLarge05(BenchRootTime parent, BenchLarge04 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }
}
