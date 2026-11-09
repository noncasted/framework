namespace Internal
{
    // A scene of a content directory manifest (LoadableScenes): the GUID of the scene asset and its serialized file.
    public readonly struct ContentManifestScene
    {
        public ContentManifestScene(string guid, string path, int serializedFile)
        {
            Guid = guid;
            Path = path;
            SerializedFile = serializedFile;
        }

        public string Guid { get; }
        public string Path { get; }
        public int SerializedFile { get; }
    }
}
