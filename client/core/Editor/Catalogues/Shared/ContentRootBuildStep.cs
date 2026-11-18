using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Internal
{
    // The ContentRoot follows the catalogues and the scene folders of the build, whatever the editor scheduled before:
    // generated before the bundles are laid out (a bundle holds it when it is among its roots). A build that leaves bundles
    // out lays out and builds the others with a ContentRoot without the groups and scenes that are their roots, then the
    // project gets the full one back; the layout phase knows every bundle left out, whatever the order of the steps.
    public sealed class ContentRootBuildStep : IContentBuildStep, IContentLayoutStep
    {
        public void BeforeContentBuild(ContentBuildContext build)
        {
            ContentRootGenerator.GenerateNow();
        }

        public void BeforeLayout(ContentBuildContext build)
        {
            if (build.ExcludedBundles.Count > 0)
                ContentRootGenerator.GenerateWithout(ExcludedRoots(build.ExcludedBundles));
        }

        public void AfterBundles(ContentBuildContext build)
        {
            if (build.ExcludedBundles.Count > 0)
                ContentRootGenerator.GenerateNow();
        }

        // The paths of the roots of these bundles.
        private static IReadOnlyCollection<string> ExcludedRoots(IReadOnlyList<ContentBundle> bundles)
        {
            return new HashSet<string>(bundles.SelectMany(bundle => bundle.Roots)
                .Where(root => root != null)
                .Select(AssetDatabase.GetAssetPath));
        }
    }
}
