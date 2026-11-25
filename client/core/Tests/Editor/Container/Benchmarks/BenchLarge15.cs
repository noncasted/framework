namespace Internal.Tests
{
    internal sealed class BenchLarge15 : IBenchEntityComponent
    {
        public readonly BenchLarge14 Previous;
        public readonly BenchLarge05 Far;

        public BenchLarge15(BenchLarge14 previous, BenchLarge05 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
