namespace Internal.Tests
{
    internal sealed class BenchLarge10
    {
        public readonly BenchMatchScore Parent;
        public readonly BenchLarge09 Previous;

        public BenchLarge10(BenchMatchScore parent, BenchLarge09 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }
}
