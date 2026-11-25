namespace Internal.Tests
{
    internal sealed class BenchLarge28 : IBenchSetup, IBenchLoaded
    {
        public readonly BenchLarge27 Previous;
        public readonly BenchLarge18 Far;

        public BenchLarge28(BenchLarge27 previous, BenchLarge18 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
