namespace Internal.Tests
{
    internal sealed class BenchMatchScore
    {
        public readonly BenchMatchState State;
        public readonly BenchMatchRules Rules;

        public BenchMatchScore(BenchMatchState state, BenchMatchRules rules)
        {
            State = state;
            Rules = rules;
        }
    }
}
