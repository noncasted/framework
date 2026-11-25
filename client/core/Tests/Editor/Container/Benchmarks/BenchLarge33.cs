namespace Internal.Tests
{
    internal sealed class BenchLarge33 : IBenchEntityComponent
    {
        public readonly BenchLarge32 Previous;
        public readonly BenchLarge23 Far;

        public BenchLarge33(BenchLarge32 previous, BenchLarge23 far)
        {
            Previous = previous;
            Far = far;
        }
    }
}
