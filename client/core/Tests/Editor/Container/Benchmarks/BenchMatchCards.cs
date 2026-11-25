namespace Internal.Tests
{
    internal sealed class BenchMatchCards : IBenchSetup
    {
        public readonly BenchMatchPlayers Players;
        public readonly BenchMatchBoard Board;
        public readonly BenchRootPrefabs Prefabs;

        public BenchMatchCards(
            BenchMatchPlayers players,
            BenchMatchBoard board,
            BenchRootPrefabs prefabs)
        {
            Players = players;
            Board = board;
            Prefabs = prefabs;
        }
    }
}
