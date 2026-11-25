namespace Internal.Tests
{
    internal sealed class BenchLarge09 : IBenchEntityComponent
    {
        public readonly BenchRootLog Parent;
        public readonly BenchLarge08 Previous;

        public BenchLarge09(BenchRootLog parent, BenchLarge08 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }
}
