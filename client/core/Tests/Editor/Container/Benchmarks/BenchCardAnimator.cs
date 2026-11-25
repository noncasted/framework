namespace Internal.Tests
{
    internal sealed class BenchCardAnimator : IBenchEntityComponent
    {
        public readonly BenchRootTime Time;
        public readonly BenchCardDefinition Definition;

        public BenchCardAnimator(BenchRootTime time, BenchCardDefinition definition)
        {
            Time = time;
            Definition = definition;
        }
    }
}
