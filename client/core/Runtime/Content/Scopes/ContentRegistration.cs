namespace Internal
{
    // When AddContentBundle registers a bundle: at once in the background, or at the first IContentLoadersCollection.Wait
    // of its name.
    public enum ContentRegistration
    {
        Background = 0,
        OnFirstWait = 1
    }
}
