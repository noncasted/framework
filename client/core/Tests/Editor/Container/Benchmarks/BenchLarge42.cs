namespace Internal.Tests
{
    internal sealed class BenchLarge42 : IBenchEntityComponent, IBenchLoaded
    {
        public readonly BenchLarge32 Near;
        public readonly BenchLarge22 Far;

        public BenchLarge42(BenchLarge32 near, BenchLarge22 far)
        {
            Near = near;
            Far = far;
        }
    }
}
