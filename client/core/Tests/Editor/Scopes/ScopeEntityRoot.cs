namespace Internal.Tests
{
    public sealed class ScopeEntityRoot
    {
        public ScopeEntityRoot(ILifetime lifetime)
        {
            Lifetime = lifetime;
        }

        public ILifetime Lifetime { get; }
    }
}
