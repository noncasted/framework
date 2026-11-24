namespace Internal
{
    // The only phase of a scope: called once the container is built, with the scope lifetime (an entity: a child of it that
    // its teardown ends first). A listener subscribes and registers on that lifetime here; its end comes while the
    // container and the asset groups of the scope are alive. What it needs to be ready is done in its constructor: the container builds the
    // dependencies of a service before the service, so a constructor may read them, and OnSetup may use any service of
    // the scope. An order that the dependencies do not give is a call of the owner, written where it loads the scope.
    public interface IScopeSetup
    {
        void OnSetup(IReadOnlyLifetime lifetime);
    }
}
