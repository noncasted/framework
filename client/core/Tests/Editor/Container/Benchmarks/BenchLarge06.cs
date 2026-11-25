namespace Internal.Tests
{
    internal sealed class BenchLarge06 : IBenchEntityComponent
    {
        public readonly BenchMatchRules Parent;
        public readonly BenchLarge05 Previous;

        public BenchLarge06(BenchMatchRules parent, BenchLarge05 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }
}
