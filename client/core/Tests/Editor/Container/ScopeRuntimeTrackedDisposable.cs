namespace Internal.Tests
{
    public sealed class ScopeRuntimeTrackedDisposable : System.IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
