namespace Internal
{
    // The kind of a serialized file of a content directory manifest (Id.Kind; ContentFileSourceType of the engine).
    public enum ContentFileKind
    {
        // All the objects of an asset in one file: a ScriptableObject, a prefab, a scene.
        ObjectCluster = 1,

        // One object of an imported asset (a texture, a sprite, a clip), with its local id.
        SingleImportedObject = 2,

        // One object of a source asset.
        SingleSourceObject = 3,

        // The scripts the serialized files reference.
        MonoScript = 4,

        // The default resources of the player: no content hash, not copied into the directory.
        DefaultResources = 5
    }
}
