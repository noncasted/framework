namespace Internal.Tests
{
    internal sealed class BenchCardAction : IBenchSetup
    {
        public readonly BenchCardDefinition Definition;
        public readonly BenchMatchRules Rules;
        public readonly BenchMatchScore Score;

        public BenchCardAction(
            BenchCardDefinition definition,
            BenchMatchRules rules,
            BenchMatchScore score)
        {
            Definition = definition;
            Rules = rules;
            Score = score;
        }
    }
}
