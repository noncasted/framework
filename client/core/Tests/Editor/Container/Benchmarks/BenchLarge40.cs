namespace Internal.Tests
{
    internal sealed class BenchLarge40 : IBenchSetup
    {
        public readonly BenchLarge39 Previous;
        public readonly BenchLarge30 Far;

        public BenchLarge40(BenchLarge39 previous, BenchLarge30 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
