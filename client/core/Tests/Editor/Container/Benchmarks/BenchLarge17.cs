namespace Internal.Tests
{
    internal sealed class BenchLarge17
    {
        public readonly BenchLarge16 Previous;
        public readonly BenchLarge07 Far;

        public BenchLarge17(BenchLarge16 previous, BenchLarge07 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
