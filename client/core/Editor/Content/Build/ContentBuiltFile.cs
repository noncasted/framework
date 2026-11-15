namespace Internal
{
    // A file of a built bundle.
    public sealed class ContentBuiltFile
    {
        public ContentBuiltFile(string path, string name, long size, bool isScenes)
        {
            Path = path;
            Name = name;
            Size = size;
            IsScenes = isScenes;
        }

        // Where the build wrote it.
        public string Path { get; }

        // The name of the build, under StreamingAssets/Content.
        public string Name { get; }

        public long Size { get; }
        public bool IsScenes { get; }
    }
}
