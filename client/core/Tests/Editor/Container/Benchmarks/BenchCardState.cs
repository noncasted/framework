namespace Internal.Tests
{
    internal sealed class BenchCardState
    {
        public readonly BenchCardDefinition Definition;
        public readonly BenchMatchState MatchState;

        public BenchCardState(BenchCardDefinition definition, BenchMatchState matchState)
        {
            Definition = definition;
            MatchState = matchState;
        }
    }
}
