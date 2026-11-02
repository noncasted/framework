namespace Internal
{
    // One binding declared in a prefab: where it stands in the order of the batch and the request that regenerates it.
    internal sealed class HierarchyBindingsPrefabItem
    {
        public HierarchyBindingsPrefabItem(
            int variantDepth,
            int hierarchyDepth,
            string fullTypeName,
            HierarchyBindingsRequest request)
        {
            VariantDepth = variantDepth;
            HierarchyDepth = hierarchyDepth;
            FullTypeName = fullTypeName;
            Request = request;
        }

        public int VariantDepth { get; }
        public int HierarchyDepth { get; }
        public string FullTypeName { get; }
        public HierarchyBindingsRequest Request { get; }
    }
}
