namespace Internal
{
    public readonly struct ContainerGraphAsset
    {
        public readonly string AssetPath;
        public readonly string HolderType;
        public readonly string[] ComponentTypes;

        public ContainerGraphAsset(string assetPath, string holderType, string[] componentTypes)
        {
            AssetPath = assetPath ?? "";
            HolderType = holderType ?? "";
            ComponentTypes = componentTypes ?? new string[0];
        }
    }
}
