namespace Internal.Tests
{
    internal sealed class BenchCardId : IBenchEntityComponent
    {
        public readonly BenchMatchState State;

        public BenchCardId(BenchMatchState state)
        {
            State = state;
        }
    }
}
