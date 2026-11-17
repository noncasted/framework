using System.Collections.Generic;

namespace Internal
{
    /// <summary>
    ///     The shared content bundles of the game, which <see cref="ContentSharedSync" /> writes: an asset several bundles
    ///     need directly and none holds (the engine would copy it into each) becomes a root of one of them. At most one
    ///     non-abstract class of the project implements it, created with its parameterless constructor; the sync runs before
    ///     every content build (after the <see cref="IContentBuildStep" />s and the ContentRoot), in Regenerate All and from
    ///     Tools/Framework/Codegen/Sync Shared Content Bundles.
    /// </summary>
    public interface IContentSharedBundles
    {
        // The folder of the game (Assets/...) the shared ContentBundle assets are written into, as <Folder>/<Name>.asset.
        string Folder { get; }

        // In the order an asset looks for its shared bundle (ContentSharedBundle.Takes).
        IReadOnlyList<ContentSharedBundle> Bundles { get; }
    }
}
