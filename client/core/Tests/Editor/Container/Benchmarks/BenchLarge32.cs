namespace Internal.Tests
{
    internal sealed class BenchLarge32 : IBenchSetup
    {
        public readonly BenchLarge31 Previous;
        public readonly BenchLarge22 Far;

        public BenchLarge32(BenchLarge31 previous, BenchLarge22 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
