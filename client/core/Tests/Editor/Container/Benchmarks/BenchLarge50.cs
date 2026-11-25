namespace Internal.Tests
{
    internal sealed class BenchLarge50
    {
        public readonly BenchLarge40 Near;
        public readonly BenchLarge30 Far;

        public BenchLarge50(BenchLarge40 near, BenchLarge30 far)
        {
            Near = near;
            Far = far;
        }
    }
}
