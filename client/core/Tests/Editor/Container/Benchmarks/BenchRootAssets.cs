namespace Internal.Tests
{
    internal sealed class BenchRootAssets : IBenchSceneService
    {
        public readonly BenchRootConfig Config;

        public BenchRootAssets(BenchRootConfig config)
        {
            Config = config;
        }
    }
}
