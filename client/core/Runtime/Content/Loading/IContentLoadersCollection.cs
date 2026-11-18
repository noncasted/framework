using Cysharp.Threading.Tasks;

namespace Internal
{
    // The bundles the root scope registered: the main one of AddContent (the catalogues load from it) and the ones of
    // AddContentBundle, each loaded whole and held while the root lives, each with its own loader (its roots: RootAssets).
    // Any loader loads any reference: the catalog knows the bundle of every lazy target.
    public interface IContentLoadersCollection
    {
        // The loader of the bundle of this name; an unknown name throws, and so does a bundle still registering (Wait for it).
        IContentLoader Get(string name);

        // The loader once the bundle is registered: AddContentBundle registers in the background while the scopes after
        // the root are built. Its failed registration throws here; an unknown name throws at once.
        UniTask<IContentLoader> Wait(IReadOnlyLifetime lifetime, string name);
    }
}
