namespace Internal.Tests
{
    internal sealed class BenchLarge25
    {
        public readonly BenchLarge24 Previous;
        public readonly BenchLarge15 Far;

        public BenchLarge25(BenchLarge24 previous, BenchLarge15 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
