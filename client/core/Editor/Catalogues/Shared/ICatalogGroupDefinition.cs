namespace Internal
{
    // Common part of group definitions: the group name and the Addressables address of its asset.
    internal interface ICatalogGroupDefinition
    {
        string Name { get; }
        string Address { get; set; }
    }
}