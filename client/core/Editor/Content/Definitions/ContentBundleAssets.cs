using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Internal
{
    // The ContentBundle assets of the project, the bundles every player build builds: in the order of their names; two
    // of one name (case aside: the engine lowercases the names of bundles) or a name that is no file name throw.
    public static class ContentBundleAssets
    {
        public static IReadOnlyList<ContentBundle> All()
        {
            var bundles = AssetDatabase.FindAssets($"t:{nameof(ContentBundle)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ContentBundle>)
                .Where(bundle => bundle != null)
                .OrderBy(bundle => bundle.Name, StringComparer.Ordinal)
                .ToArray();

            Check(bundles);
            return bundles;
        }

        // The bundle of this name.
        public static ContentBundle Of(string name)
        {
            var bundles = All();
            return bundles.SingleOrDefault(bundle => bundle.Name == name) ??
                   throw new InvalidOperationException(
                       $"[Content] No ContentBundle asset {name}: the project has {string.Join(", ", bundles.Select(bundle => bundle.Name))}");
        }

        public static void Check(IReadOnlyList<ContentBundle> bundles)
        {
            var twice = bundles.GroupBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);

            if (twice != null)
            {
                throw new InvalidOperationException(
                    $"[Content] Two content bundles are named {twice.Key}: {string.Join(", ", twice.Select(AssetDatabase.GetAssetPath))}");
            }

            foreach (var bundle in bundles)
            {
                if (bundle.Name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-') == false)
                    throw new InvalidOperationException($"[Content] The content bundle {AssetDatabase.GetAssetPath(bundle)}: a name of letters, digits, '_' and '-' only");
            }
        }
    }
}
