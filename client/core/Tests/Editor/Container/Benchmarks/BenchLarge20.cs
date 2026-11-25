namespace Internal.Tests
{
    internal sealed class BenchLarge20 : IBenchSetup
    {
        public readonly BenchLarge19 Previous;
        public readonly BenchLarge10 Far;

        public BenchLarge20(BenchLarge19 previous, BenchLarge10 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
