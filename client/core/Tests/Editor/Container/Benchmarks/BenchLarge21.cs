namespace Internal.Tests
{
    internal sealed class BenchLarge21 : IBenchEntityComponent, IBenchLoaded
    {
        public readonly BenchLarge20 Previous;
        public readonly BenchLarge11 Far;

        public BenchLarge21(BenchLarge20 previous, BenchLarge11 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
