namespace Internal.Tests
{
    internal sealed class BenchCardDefinition
    {
        public readonly BenchMatchCards Cards;
        public readonly BenchRootConfig Config;

        public BenchCardDefinition(BenchMatchCards cards, BenchRootConfig config)
        {
            Cards = cards;
            Config = config;
        }
    }
}
