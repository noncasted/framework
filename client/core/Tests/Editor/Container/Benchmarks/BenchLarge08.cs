namespace Internal.Tests
{
    internal sealed class BenchLarge08 : IBenchSetup
    {
        public readonly BenchMatchCards Parent;
        public readonly BenchLarge07 Previous;

        public BenchLarge08(BenchMatchCards parent, BenchLarge07 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }
}
