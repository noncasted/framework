namespace Internal.Tests
{
    internal sealed class BenchLarge49 : IBenchLoaded
    {
        public readonly BenchLarge39 Near;
        public readonly BenchLarge29 Far;

        public BenchLarge49(BenchLarge39 near, BenchLarge29 far)
        {
            Near = near;
            Far = far;
        }
    }
}
