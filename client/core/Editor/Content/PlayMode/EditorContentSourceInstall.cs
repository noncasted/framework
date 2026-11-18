using System;
using System.Linq;
using UnityEditor;

namespace Internal
{
    // Gives AddContent and AddContentBundle of Play Mode the bundles of the project: the roots of the ContentBundle asset
    // of the name, as assets of the project.
    internal static class EditorContentSourceInstall
    {
        [InitializeOnLoadMethod]
        private static void Install()
        {
            EditorContentSourceFactory.Install(Create);
        }

        private static IContentSource Create(string name)
        {
            var bundle = ContentBundleAssets.Of(name);
            var roots = bundle.Roots.ToArray();

            if (roots.Any(root => root == null))
                throw new InvalidOperationException($"[Content] The content bundle {name} has an empty root ({AssetDatabase.GetAssetPath(bundle)})");

            return new EditorContentSource(name, roots);
        }
    }
}
