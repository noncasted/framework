namespace Internal.Tests
{
    internal sealed class BenchLarge02
    {
        public readonly BenchRootConfig Parent;
        public readonly BenchLarge01 Previous;

        public BenchLarge02(BenchRootConfig parent, BenchLarge01 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }
}
