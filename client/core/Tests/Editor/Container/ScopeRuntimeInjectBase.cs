namespace Internal.Tests
{
    public abstract class ScopeRuntimeInjectBase
    {
        public ScopeRuntimeConfig Config { get; private set; }

        [Inject]
        public void Setup(ScopeRuntimeConfig config)
        {
            Config = config;
        }
    }
}
