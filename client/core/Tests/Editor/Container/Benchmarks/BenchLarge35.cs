namespace Internal.Tests
{
    internal sealed class BenchLarge35 : IBenchLoaded
    {
        public readonly BenchLarge34 Previous;
        public readonly BenchLarge25 Far;

        public BenchLarge35(BenchLarge34 previous, BenchLarge25 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
