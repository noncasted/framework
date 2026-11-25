namespace Internal.Tests
{
    internal sealed class BenchLarge03 : IBenchEntityComponent
    {
        public readonly BenchMatchBoard Parent;
        public readonly BenchLarge02 Previous;

        public BenchLarge03(BenchMatchBoard parent, BenchLarge02 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }
}
