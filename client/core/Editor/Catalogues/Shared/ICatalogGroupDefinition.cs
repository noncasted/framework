namespace Internal
{
    // Common part of group definitions: the group name and the GUID of its asset (its key in the ContentRoot).
    internal interface ICatalogGroupDefinition
    {
        string Name { get; }
        string Guid { get; }
        void SetGuid(string guid);
    }
}