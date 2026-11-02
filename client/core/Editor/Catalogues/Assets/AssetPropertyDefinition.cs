namespace Internal
{
    internal sealed class AssetPropertyDefinition
    {
        public AssetPropertyDefinition(
            string propertyName,
            string fieldName,
            EnvAsset asset,
            string typeFullName,
            string typeAssembly)
        {
            PropertyName = propertyName;
            FieldName = fieldName;
            Asset = asset;
            TypeFullName = typeFullName;
            TypeAssembly = typeAssembly;
        }

        public string PropertyName { get; }
        public string FieldName { get; }
        public EnvAsset Asset { get; }
        public string TypeFullName { get; }
        public string TypeAssembly { get; }
    }
}
