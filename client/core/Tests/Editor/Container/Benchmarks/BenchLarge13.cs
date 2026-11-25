namespace Internal.Tests
{
    internal sealed class BenchLarge13
    {
        public readonly BenchLarge12 Previous;
        public readonly BenchLarge03 Far;

        public BenchLarge13(BenchLarge12 previous, BenchLarge03 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
