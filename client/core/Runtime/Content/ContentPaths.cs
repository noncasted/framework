namespace Internal
{
    // Where a player build puts a content directory and its index: the files in StreamingAssets/Content/<name>, the
    // index as the json TextAsset Resources/Content/<name>, read synchronously at the start without a request.
    public static class ContentPaths
    {
        public const string Folder = "Content";

        // The only file of a directory not named by its hash: the store writes it from ContentIndex.ManifestHash.
        public const string ManifestHashFile = "BuildManifestHash.txt";

        // The extension of a file shipped compressed with brotli (ContentIndex.IsBrotli), as Build/*.br of Unity.
        public const string BrotliExtension = ".br";

        // Under StreamingAssets.
        public static string Directory(string name)
        {
            return $"{Folder}/{name}";
        }

        // The path of Resources.Load<TextAsset>.
        public static string IndexResource(string name)
        {
            return $"{Folder}/{name}";
        }
    }
}
