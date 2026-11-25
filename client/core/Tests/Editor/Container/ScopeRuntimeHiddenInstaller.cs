namespace Internal.Tests
{
    public sealed class ScopeRuntimeHiddenInstaller : IScopeRuntimeInstaller
    {
        public void Install(IBuilder builder)
        {
            builder.Register<ScopeRuntimeHiddenService>();
        }
    }
}
