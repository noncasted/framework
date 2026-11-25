namespace Internal.Tests
{
    public sealed class ScopeRuntimeSecondConsumer
    {
        public ScopeRuntimeSecondConsumer(ScopeRuntimeConfig config)
        {
            Config = config;
        }

        public ScopeRuntimeConfig Config { get; }
    }
}
