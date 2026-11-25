namespace Internal.Tests
{
    internal sealed class BenchLarge16 : IBenchSetup
    {
        public readonly BenchLarge15 Previous;
        public readonly BenchLarge06 Far;

        public BenchLarge16(BenchLarge15 previous, BenchLarge06 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
