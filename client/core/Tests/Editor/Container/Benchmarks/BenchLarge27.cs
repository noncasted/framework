namespace Internal.Tests
{
    internal sealed class BenchLarge27 : IBenchEntityComponent
    {
        public readonly BenchLarge26 Previous;
        public readonly BenchLarge17 Far;

        public BenchLarge27(BenchLarge26 previous, BenchLarge17 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
