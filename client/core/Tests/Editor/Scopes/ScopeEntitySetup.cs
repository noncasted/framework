namespace Internal.Tests
{
    public sealed class ScopeEntitySetup : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            ScopeLoaderProbe.Log.Add("entity.setup");
            lifetime.Listen(() => ScopeLoaderProbe.Log.Add("entity.dispose"));
        }
    }
}
