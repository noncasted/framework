namespace Internal.Tests
{
    internal sealed class BenchLarge31
    {
        public readonly BenchLarge30 Previous;
        public readonly BenchLarge21 Far;

        public BenchLarge31(BenchLarge30 previous, BenchLarge21 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
