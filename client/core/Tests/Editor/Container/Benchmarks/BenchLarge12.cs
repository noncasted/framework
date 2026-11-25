namespace Internal.Tests
{
    internal sealed class BenchLarge12 : IBenchEntityComponent, IBenchSetup
    {
        public readonly BenchLarge11 Previous;
        public readonly BenchLarge02 Far;

        public BenchLarge12(BenchLarge11 previous, BenchLarge02 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
