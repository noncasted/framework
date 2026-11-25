namespace Internal.Tests
{
    internal sealed class BenchLarge45 : IBenchEntityComponent
    {
        public readonly BenchLarge35 Near;
        public readonly BenchLarge25 Far;

        public BenchLarge45(BenchLarge35 near, BenchLarge25 far)
        {
            Near = near;
            Far = far;
        }
    }
}
