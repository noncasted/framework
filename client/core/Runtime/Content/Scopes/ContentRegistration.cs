namespace Internal
{
    // When AddContentDirectory registers an extra directory: at once in the background, or at the first
    // IContentDirectories.Wait of its name.
    public enum ContentRegistration
    {
        Background = 0,
        OnFirstWait = 1
    }
}
