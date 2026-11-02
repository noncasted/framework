namespace Internal
{
    // The targets of an updater, by phase.
    internal sealed class UpdateTargets
    {
        public readonly ModifiableList<UpdateTargetHandle<IPreFixedUpdatable>> PreFixed = new();
        public readonly ModifiableList<UpdateTargetHandle<IFixedUpdatable>> Fixed = new();
        public readonly ModifiableList<UpdateTargetHandle<IPostFixedUpdatable>> PostFixed = new();
        public readonly ModifiableList<UpdateTargetHandle<IPreUpdatable>> PreUpdate = new();
        public readonly ModifiableList<UpdateTargetHandle<IUpdatable>> Update = new();
        public readonly ModifiableList<UpdateTargetHandle<IGizmosUpdatable>> Gizmos = new();
        public readonly ModifiableList<UpdateTargetHandle<IPostUpdatable>> PostUpdate = new();
        public readonly ModifiableList<UpdateTargetHandle<ILateUpdatable>> LateUpdate = new();
    }
}
