namespace Internal.Tests
{
    internal sealed class BenchLarge11
    {
        public readonly BenchLarge10 Previous;
        public readonly BenchLarge01 Far;

        public BenchLarge11(BenchLarge10 previous, BenchLarge01 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
