namespace Internal.Tests
{
    internal sealed class BenchLarge39 : IBenchEntityComponent
    {
        public readonly BenchLarge38 Previous;
        public readonly BenchLarge29 Far;

        public BenchLarge39(BenchLarge38 previous, BenchLarge29 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
