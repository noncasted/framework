namespace Internal.Tests
{
    public sealed class ScopeRuntimeThrowingDisposable : System.IDisposable
    {
        public void Dispose()
        {
            throw new System.InvalidOperationException("dispose failed");
        }
    }
}
