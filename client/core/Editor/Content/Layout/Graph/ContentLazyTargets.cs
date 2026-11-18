using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // The target of a lazy reference as a bundle loads it: its path, and the name of a sub-asset.
    public static class ContentLazyTargets
    {
        // The path of the target, null for an empty or broken reference.
        public static string PathOf(ContentLazyReference reference)
        {
            if (reference.IsEmpty)
                return null;

            var path = AssetDatabase.GUIDToAssetPath(reference.Guid);

            if (string.IsNullOrEmpty(path) || File.Exists(path) == false)
                return null;

            if (reference.IsScene)
                return ContentAssetGraph.IsScene(path) ? path : null;

            return ContentEditorAssets.Find(reference.Guid, reference.LocalId) == null ? null : path;
        }

        // The name a sub-asset is loaded by, empty for the main object or a component of the root of a prefab; null with
        // the error for an object the loader cannot reach.
        public static string SubName(Object target, string path, out string error)
        {
            error = null;
            var main = AssetDatabase.LoadMainAssetAtPath(path);

            if (target == main)
                return string.Empty;

            if (target is Component component)
            {
                if (main is GameObject prefab && component.gameObject == prefab)
                    return string.Empty;

                error = $"{component.GetType().Name} of {component.gameObject.name} in {path} is on a child of the prefab: point at its root";
                return null;
            }

            if (target is GameObject)
            {
                error = $"{target.name} in {path} is a child of the prefab: point at its root";
                return null;
            }

            var twins = AssetDatabase.LoadAllAssetsAtPath(path).Count(asset => asset != null && asset.name == target.name && asset.GetType() == target.GetType());

            if (twins > 1)
            {
                error = $"{path} has {twins} sub-assets {target.GetType().Name} {target.name}: the loader finds a sub-asset by its name";
                return null;
            }

            return target.name;
        }
    }
}
