namespace Internal.Tests
{
    internal sealed class BenchRootAudio : IBenchLoaded
    {
        public readonly BenchRootPrefabs Prefabs;
        public readonly BenchRootLog Log;

        public BenchRootAudio(BenchRootPrefabs prefabs, BenchRootLog log)
        {
            Prefabs = prefabs;
            Log = log;
        }
    }
}
