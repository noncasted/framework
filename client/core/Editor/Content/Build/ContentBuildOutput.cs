namespace Internal
{
    // The content directory prepared for the running player build: its folders in Library, its index and its stamp.
    public sealed class ContentBuildOutput
    {
        public ContentBuildOutput(ContentDirectorySettings directory, string folder, string shipped, ContentIndex index, ContentStamp stamp,
            string reportDirectory)
        {
            Directory = directory;
            Folder = folder;
            Shipped = shipped;
            Index = index;
            Stamp = stamp;
            ReportDirectory = reportDirectory;
        }

        public ContentDirectorySettings Directory { get; }

        // Library/Content/<name>: the directory as BuildContentDirectory wrote it.
        public string Folder { get; }

        // What the player gets as StreamingAssets/Content/<name> (ContentShipment): the folder of the archive with the
        // index, or Library/ContentShipped/<name> for a fetching WebGL player.
        public string Shipped { get; }

        public ContentIndex Index { get; }

        // What the player gets in Resources.
        public ContentStamp Stamp { get; }

        // The build report directory of the content build: the player build reads the types of the directory from it.
        public string ReportDirectory { get; }
    }
}
