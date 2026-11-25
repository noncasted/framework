namespace Internal.Tests
{
    internal sealed class BenchLarge48 : IBenchEntityComponent, IBenchSetup
    {
        public readonly BenchLarge38 Near;
        public readonly BenchLarge28 Far;

        public BenchLarge48(BenchLarge38 near, BenchLarge28 far)
        {
            Near = near;
            Far = far;
        }
    }
}
