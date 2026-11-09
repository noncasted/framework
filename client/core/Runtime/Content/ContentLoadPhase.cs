namespace Internal
{
    // Where the load of a content reference is: its files on their way, the engine loading it, loaded, released.
    internal enum ContentLoadPhase
    {
        Fetching,
        Loading,
        Loaded,
        Over
    }
}
