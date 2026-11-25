namespace Internal.Tests
{
    internal sealed class BenchRootSaves : IBenchDispose
    {
        public readonly BenchRootConfig Config;

        public BenchRootSaves(BenchRootConfig config)
        {
            Config = config;
        }
    }
}
