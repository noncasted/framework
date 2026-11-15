namespace Internal
{
    // A lazy reference found in an asset or a scene: where it is (for the errors), the GUID and the local id of its target
    // (0 for a scene) and whether it is a ContentScene.
    public readonly struct ContentLazyReference
    {
        public ContentLazyReference(string where, string guid, long localId, bool isScene)
        {
            Where = where;
            Guid = guid;
            LocalId = localId;
            IsScene = isScene;
        }

        public string Where { get; }
        public string Guid { get; }
        public long LocalId { get; }
        public bool IsScene { get; }
        public bool IsEmpty => string.IsNullOrEmpty(Guid);
    }
}
