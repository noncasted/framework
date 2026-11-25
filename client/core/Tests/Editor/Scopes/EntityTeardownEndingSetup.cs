namespace Internal.Tests
{
    public sealed class EntityTeardownEndingSetup : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            EntityTeardownRoots.ParentLifetime.Terminate();
        }
    }
}
