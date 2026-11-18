using System.Collections.Generic;

namespace Internal
{
    // A bundle as BuildAssetBundles wrote it: its files (the built path and the name of the build, <name>.<hash>.bundle)
    // and the bundles its files depend on.
    public sealed class ContentBuiltBundle
    {
        public ContentBuiltBundle(ContentBundlePlan plan, IReadOnlyList<ContentBuiltFile> files, IReadOnlyList<string> dependencies)
        {
            Plan = plan;
            Files = files;
            Dependencies = dependencies;
        }

        public ContentBundlePlan Plan { get; }
        public string Name => Plan.Name;
        public IReadOnlyList<ContentBuiltFile> Files { get; }
        public IReadOnlyList<string> Dependencies { get; }
    }
}
