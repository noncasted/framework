namespace Internal
{
    // Where a player build puts the content: every file of every bundle in StreamingAssets/Content, named by its hash,
    // and the catalog in Resources (ContentCatalog.Resource). A bundle shipped in gzip for a WebGL player is <file>.gz.
    public static class ContentCatalogPaths
    {
        public const string Folder = "Content";

        public const string GzipExtension = ".gz";

        // The name on the server of a file of a bundle.
        public static string Shipped(ContentCatalogBundle bundle, ContentCatalogFile file)
        {
            return bundle.IsGzip ? file.Name + GzipExtension : file.Name;
        }
    }
}
