namespace Internal.Tests
{
    internal sealed class BenchLarge22
    {
        public readonly BenchLarge21 Previous;
        public readonly BenchLarge12 Far;

        public BenchLarge22(BenchLarge21 previous, BenchLarge12 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
