namespace Internal.Tests
{
    // A plain entity view: no prefab, no components.
    public sealed class ScopeTestEntityView : IScopeEntityView
    {
        public void CreateViews(IEntityBuilder builder)
        {
        }

        public void Bind(IContainer container)
        {
        }
    }
}
