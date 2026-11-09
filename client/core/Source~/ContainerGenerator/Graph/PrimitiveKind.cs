namespace ContainerGenerator {
    // Mirror of Internal.ContainerPrimitive (client/core/Runtime/Tools/Container/Generated/ContainerPrimitiveAttribute.cs):
    // the builder API marks its registration primitives with [ContainerPrimitive(kind)], the walker reads the kind.
    internal enum PrimitiveKind {
        None = 0,
        Register = 1,
        RegisterInstance = 2,
        RegisterComponent = 3,
        As = 4,
        AsSelf = 5,
        AsSelfResolvable = 6,
        WithParameter = 7,
        WithScopeLifetime = 8,
        Inject = 9,
        Injectable = 10,
        Instantiate = 11,
        SceneServices = 12,
        Content = 13,
    }
}
