namespace Internal
{
    // Where a player build puts a content directory: StreamingAssets/Content/<name> holds its archive and its index, and
    // the stamp (ContentStamp) is the json TextAsset Resources/Content/<name>, read synchronously at the start.
    // A player that reads the folder in place gets the archive as the registration mounts it (content0.archive next to
    // BuildManifestHash.txt); a WebGL player that fetches it gets <hash>.archive, named by the hash of the build, and
    // writes the two files of the registration itself.
    public static class ContentPaths
    {
        public const string Folder = "Content";

        // The only file of a directory not named by its hash: a fetching store writes it from ContentIndex.ManifestHash.
        public const string ManifestHashFile = "BuildManifestHash.txt";

        // The archive the registration mounts: the engine looks for content<n>.archive in the folder.
        public const string ArchiveFile = "content0.archive";

        // The extension of a file shipped compressed with brotli (the index of a WebGL build with brotli), as Build/*.br.
        public const string BrotliExtension = ".br";

        // Under StreamingAssets.
        public static string Directory(string name)
        {
            return $"{Folder}/{name}";
        }

        // The path of Resources.Load<TextAsset> of the stamp.
        public static string StampResource(string name)
        {
            return $"{Folder}/{name}";
        }

        // The index of the build with this manifest hash (ContentIndex.ManifestHash), in the folder of the directory.
        public static string IndexFile(string manifestHash)
        {
            return $"{manifestHash}.index.json";
        }

        // The archive a WebGL player fetches: a new name for every build, so the server and the Cache API keep it forever.
        public static string ShippedArchive(string manifestHash)
        {
            return $"{manifestHash}.archive";
        }
    }
}
