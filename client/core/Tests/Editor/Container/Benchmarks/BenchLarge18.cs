namespace Internal.Tests
{
    internal sealed class BenchLarge18 : IBenchEntityComponent
    {
        public readonly BenchLarge17 Previous;
        public readonly BenchLarge08 Far;

        public BenchLarge18(BenchLarge17 previous, BenchLarge08 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
