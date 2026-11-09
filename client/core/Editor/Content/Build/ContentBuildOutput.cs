namespace Internal
{
    // The content directory prepared for the running player build: its folder in Library and its index.
    public sealed class ContentBuildOutput
    {
        public ContentBuildOutput(ContentDirectorySettings directory, string folder, ContentIndex index)
        {
            Directory = directory;
            Folder = folder;
            Index = index;
        }

        public ContentDirectorySettings Directory { get; }

        // Library/Content/<name>: what the player gets as StreamingAssets/Content/<name>.
        public string Folder { get; }

        public ContentIndex Index { get; }
    }
}
