namespace Internal
{
    // Where the load of a content reference is: its bundle and the object on their way, loaded, let go.
    internal enum ContentLoadPhase
    {
        Loading,
        Loaded,
        Over
    }
}
