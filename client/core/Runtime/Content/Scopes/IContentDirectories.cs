namespace Internal
{
    // The content directories the root scope registered: the one of AddContent (the catalogues load from it) and the
    // extra ones of AddContentDirectory, each with its own loader. An object of a directory is loaded by the loader of
    // that directory, from a reference of its roots (IContentLoader.RootAssets) or of an asset it loaded.
    public interface IContentDirectories
    {
        // The loader of the directory of this name (ContentOptions.Name); an unknown name throws.
        IContentLoader Get(string name);
    }
}
