namespace Internal
{
    internal sealed class AssetSource
    {
        public AssetSource(string path, string group, string propertyName, EnvAsset asset)
        {
            Path = path;
            Group = group;
            PropertyName = propertyName;
            Asset = asset;
        }

        public string Path { get; }
        public string Group { get; }
        public string PropertyName { get; }
        public EnvAsset Asset { get; }
    }
}
