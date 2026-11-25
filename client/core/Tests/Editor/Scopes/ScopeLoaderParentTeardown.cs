namespace Internal.Tests
{
    // Records the end of its scope; the record of the profiler shows the stage the teardown of the scope goes to.
    public sealed class ScopeLoaderParentTeardown : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            lifetime.Listen(() => {
                GameProfiler.Scope("Parent teardown").Dispose();
                ScopeLoaderProbe.Log.Add("parent.dispose");
            });
        }
    }
}
