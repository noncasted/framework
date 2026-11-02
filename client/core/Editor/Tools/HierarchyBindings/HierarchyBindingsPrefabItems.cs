using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // The bindings declared in prefab assets, as a batch plans them.
    internal static class HierarchyBindingsPrefabItems
    {
        private const string LogTag = HierarchyBindingsLog.Tag;

        public static void CollectPrefab(string assetPath, List<HierarchyBindingsPrefabItem> items)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (asset == null)
                throw new InvalidOperationException($"{assetPath} is not a prefab.");

            var variantDepth = VariantDepth(asset);
            var contents = PrefabUtility.LoadPrefabContents(assetPath);

            try
            {
                foreach (var bindings in contents.GetComponentsInChildren<ObjectBindings>(true))
                {
                    if (HierarchyBindingsLayer.ResolveDeclaringAsset(bindings, assetPath) != assetPath)
                        continue;

                    var type = HierarchyBindingsGeneratedTypes.ResolveGeneratedType(bindings);

                    if (type == null)
                        throw new InvalidOperationException(
                            $"{bindings.GetType().Name} in {assetPath} does not derive from a generated class.");

                    items.Add(new HierarchyBindingsPrefabItem(
                        variantDepth,
                        Depth(bindings.transform),
                        type.FullName,
                        new HierarchyBindingsRequest
                        {
                            AssetPath = assetPath,
                            ObjectPath = HierarchyBindingsTarget.IndexPath(bindings.transform, contents.transform),
                            ObjectNames = HierarchyBindingsTarget.NamePath(bindings.gameObject),
                            TypeName = type.Name,
                            Namespace = type.Namespace ?? string.Empty,
                            IsPrefabAsset = true,
                            IsSceneService = bindings.IsSceneService,
                            IsEntityComponent = bindings.IsEntityComponent
                        }));
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // One class - one object: otherwise two owners would overwrite the file one after another. One file - one
        // class too: the file is named by the short type name, so ShopBindings of two namespaces generated into one
        // folder would overwrite each other.
        public static bool ValidateUniqueTypes(List<HierarchyBindingsPrefabItem> items)
        {
            var valid = true;

            foreach (var group in items.GroupBy(item => item.FullTypeName))
            {
                if (group.Count() == 1)
                    continue;

                valid = false;
                Debug.LogError($"[{LogTag}] '{group.Key}' is declared on more than one object: {Places(group)}.");
            }

            foreach (var group in items.GroupBy(item => GeneratedFilePath(item.Request)))
            {
                var types = group.Select(item => item.FullTypeName).Distinct(StringComparer.Ordinal).ToList();

                if (types.Count == 1)
                    continue;

                valid = false;
                Debug.LogError(
                    $"[{LogTag}] {string.Join(", ", types)} are all generated into {group.Key}: {Places(group)}. " +
                    "Rename all of them but one.");
            }

            return valid;
        }

        private static string Places(IEnumerable<HierarchyBindingsPrefabItem> items)
        {
            return string.Join(", ", items.Select(item => item.Request.AssetPath + ":" + item.Request.ObjectNames));
        }

        public static string GeneratedFilePath(HierarchyBindingsRequest request)
        {
            var folder = HierarchyBindingsPaths.ResolveGeneratedFolder(FindInAsset(request), request.AssetPath);
            return HierarchyBindingsPaths.ToFilePath(folder, request.TypeName);
        }

        private static int VariantDepth(GameObject asset)
        {
            var depth = 0;

            for (var current = asset;
                 PrefabUtility.GetPrefabAssetType(current) == PrefabAssetType.Variant;
                 current = PrefabUtility.GetCorrespondingObjectFromSource(current))
                depth++;

            return depth;
        }

        private static int Depth(Transform target)
        {
            var depth = 0;

            for (var current = target.parent; current != null; current = current.parent)
                depth++;

            return depth;
        }

        public static GameObject FindInAsset(HierarchyBindingsRequest request)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(request.AssetPath);

            if (asset == null)
                throw new InvalidOperationException($"{request.AssetPath} disappeared during the batch.");

            var found = HierarchyBindingsTarget.FindByIndexPath(asset.transform, request.ObjectPath);

            if (found == null)
                throw new InvalidOperationException(
                    $"'{request.ObjectNames}' disappeared from {request.AssetPath} during the batch.");

            return found.gameObject;
        }
    }
}
