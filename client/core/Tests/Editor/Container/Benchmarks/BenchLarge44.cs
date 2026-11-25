namespace Internal.Tests
{
    internal sealed class BenchLarge44 : IBenchSetup
    {
        public readonly BenchLarge34 Near;
        public readonly BenchLarge24 Far;

        public BenchLarge44(BenchLarge34 near, BenchLarge24 far)
        {
            Near = near;
            Far = far;
        }
    }
}
