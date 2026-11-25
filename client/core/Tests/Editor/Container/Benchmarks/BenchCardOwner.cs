namespace Internal.Tests
{
    internal sealed class BenchCardOwner : IBenchEntityComponent
    {
        public readonly BenchMatchPlayers Players;

        public BenchCardOwner(BenchMatchPlayers players)
        {
            Players = players;
        }
    }
}
