namespace Internal.Tests
{
    internal sealed class BenchLarge04 : IBenchSetup
    {
        public readonly BenchMatchPlayers Parent;
        public readonly BenchLarge03 Previous;

        public BenchLarge04(BenchMatchPlayers parent, BenchLarge03 previous)
        {
            Parent = parent;
            Previous = previous;
        }
    }
}
