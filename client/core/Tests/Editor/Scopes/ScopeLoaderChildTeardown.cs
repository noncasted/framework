namespace Internal.Tests
{
    public sealed class ScopeLoaderChildTeardown : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            lifetime.Listen(() => ScopeLoaderProbe.Log.Add("child.dispose"));
        }
    }
}
