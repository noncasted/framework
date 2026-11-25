namespace Internal.Tests
{
    internal sealed class BenchLarge24 : IBenchEntityComponent, IBenchSetup
    {
        public readonly BenchLarge23 Previous;
        public readonly BenchLarge14 Far;

        public BenchLarge24(BenchLarge23 previous, BenchLarge14 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
