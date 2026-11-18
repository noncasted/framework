using System;
using System.Collections.Generic;
using UnityEditor;

namespace Internal
{
    // The content build of one player build as the steps see it (IContentBuildStep, IContentLayoutStep): its target, and the
    // bundles it leaves out. A bundle left out is not built, not in the catalog and not in the ContentRoot of this build; a
    // built bundle that references what it holds fails the build (ContentBundleLayout). The project keeps the bundle: the next
    // build has it again unless a step leaves it out once more.
    public sealed class ContentBuildContext
    {
        private readonly SortedSet<string> _excluded = new(StringComparer.Ordinal);

        // Null until every IContentBuildStep has run.
        private IReadOnlyList<ContentBundle> _excludedBundles;

        public ContentBuildContext(BuildTarget target)
        {
            Target = target;
        }

        public BuildTarget Target { get; }

        // The names of the ContentBundle assets left out of this build.
        public IReadOnlyCollection<string> Excluded => _excluded;

        // The ContentBundle assets left out of this build, final once every IContentBuildStep has run (IContentLayoutStep).
        public IReadOnlyList<ContentBundle> ExcludedBundles => _excludedBundles ??
                                                               throw new InvalidOperationException("[ContentBuild] The content bundles left out are known " +
                                                                                                   "after every IContentBuildStep (IContentLayoutStep)");

        // Leaves the ContentBundle of this name out of this build; a name no bundle has fails the build.
        public void Exclude(string bundle)
        {
            if (string.IsNullOrEmpty(bundle))
                throw new ArgumentException("[ContentBuild] The name of the content bundle to leave out is empty", nameof(bundle));

            if (_excludedBundles != null)
                throw new InvalidOperationException($"[ContentBuild] The content bundle {bundle} is left out after every IContentBuildStep has run");

            _excluded.Add(bundle);
        }

        // The assets of Excluded, found by ContentBuild once every IContentBuildStep has run; nothing is left out after it.
        internal void Resolve(IReadOnlyList<ContentBundle> excludedBundles)
        {
            _excludedBundles = excludedBundles;
        }
    }
}
