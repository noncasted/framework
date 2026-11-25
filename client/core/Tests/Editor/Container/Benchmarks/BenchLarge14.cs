namespace Internal.Tests
{
    internal sealed class BenchLarge14 : IBenchLoaded
    {
        public readonly BenchLarge13 Previous;
        public readonly BenchLarge04 Far;

        public BenchLarge14(BenchLarge13 previous, BenchLarge04 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
