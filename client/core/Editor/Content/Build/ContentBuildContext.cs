using System;
using System.Collections.Generic;
using UnityEditor;

namespace Internal
{
    // The content build of one player build as the steps see it (IContentBuildStep): its target, and the bundles it leaves
    // out. A bundle left out is not built, not in the catalog and not in the ContentRoot of this build; a built bundle that
    // references what it holds fails the build (ContentBundleLayout). The project keeps the bundle: the next build has it
    // again unless a step leaves it out once more.
    public sealed class ContentBuildContext
    {
        private readonly SortedSet<string> _excluded = new(StringComparer.Ordinal);

        public ContentBuildContext(BuildTarget target)
        {
            Target = target;
        }

        public BuildTarget Target { get; }

        // The names of the ContentBundle assets left out of this build.
        public IReadOnlyCollection<string> Excluded => _excluded;

        // Leaves the ContentBundle of this name out of this build; a name no bundle has fails the build.
        public void Exclude(string bundle)
        {
            if (string.IsNullOrEmpty(bundle))
                throw new ArgumentException("[ContentBuild] The name of the content bundle to leave out is empty", nameof(bundle));

            _excluded.Add(bundle);
        }
    }
}
