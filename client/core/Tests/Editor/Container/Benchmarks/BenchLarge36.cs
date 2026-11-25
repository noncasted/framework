namespace Internal.Tests
{
    internal sealed class BenchLarge36 : IBenchEntityComponent, IBenchSetup
    {
        public readonly BenchLarge35 Previous;
        public readonly BenchLarge26 Far;

        public BenchLarge36(BenchLarge35 previous, BenchLarge26 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
