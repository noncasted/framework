namespace Internal
{
    internal readonly struct MarkedSource
    {
        public MarkedSource(string path, AudioCatalogMetadata metadata)
        {
            Path = path;
            Metadata = metadata;
        }

        public string Path { get; }
        public AudioCatalogMetadata Metadata { get; }
    }
}
