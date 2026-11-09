namespace Internal
{
    // The content directory prepared for the running player build: its folders in Library and its index.
    public sealed class ContentBuildOutput
    {
        public ContentBuildOutput(ContentDirectorySettings directory, string folder, string shipped, ContentIndex index, string reportDirectory)
        {
            Directory = directory;
            Folder = folder;
            Shipped = shipped;
            Index = index;
            ReportDirectory = reportDirectory;
        }

        public ContentDirectorySettings Directory { get; }

        // Library/Content/<name>: the directory as BuildContentDirectory wrote it.
        public string Folder { get; }

        // What the player gets as StreamingAssets/Content/<name>: the folder itself, or its brotli files
        // (Library/ContentBrotli/<name>, ContentIndex.IsBrotli).
        public string Shipped { get; }

        public ContentIndex Index { get; }

        // The build report directory of the content build: the player build reads the types of the directory from it.
        public string ReportDirectory { get; }
    }
}
