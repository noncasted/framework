namespace Internal.Tests
{
    internal sealed class BenchLarge23
    {
        public readonly BenchLarge22 Previous;
        public readonly BenchLarge13 Far;

        public BenchLarge23(BenchLarge22 previous, BenchLarge13 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
