namespace Internal.Tests
{
    // 12 marker interfaces, same consume path as EventLoop.ResolveList<T>
    // (ContainerLocal<IReadOnlyList<T>> on VContainer, ResolveAll<T> on own).
    // First 10 map to EventLoop phases; last two map to ISceneService / IEntityComponent.

    internal interface IBenchBaseSetup
    {
    }
}