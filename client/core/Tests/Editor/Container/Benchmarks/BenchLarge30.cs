namespace Internal.Tests
{
    internal sealed class BenchLarge30 : IBenchEntityComponent
    {
        public readonly BenchLarge29 Previous;
        public readonly BenchLarge20 Far;

        public BenchLarge30(BenchLarge29 previous, BenchLarge20 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
