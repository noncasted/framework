namespace Internal.Tests
{
    public sealed class ScopeEntityEndListener : IScopeSetup
    {
        public ScopeEntityEndListener(IContainer container)
        {
            _container = container;
        }

        private readonly IContainer _container;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            lifetime.Listen(() => ScopeLoaderProbe.Log.Add(
                _container.Lifetime.IsTerminated == true ? "end.containerOver" : "end.containerAlive"));
        }
    }
}
