using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // The layer of bindings is the asset they are declared in. A prefab variant carries the bindings of
    // its base together with its own ones, and each class mirrors only its own part: the base - what is
    // declared in the base, the variant - only what it added itself (objects and components, including
    // components on inherited objects). A scene and a regular prefab are one whole layer, no filter.
    //
    // The origin is resolved only on loaded contents (a prefab stage or LoadPrefabContents): there the
    // root of a variant is an instance of the base, and PrefabUtility tells reliably what is added as an
    // override and what came from the source.
    internal sealed class HierarchyBindingsLayer
    {
        private HierarchyBindingsLayer(string assetPath, bool isScene, bool isVariant, HashSet<string> variantChain)
        {
            AssetPath = assetPath;
            IsScene = isScene;
            IsVariant = isVariant;
            _variantChain = variantChain;
        }

        private readonly HashSet<string> _variantChain;

        public string AssetPath { get; }

        public bool IsScene { get; }

        // The layer filter is on only for a variant; a scene and a regular prefab are mirrored entirely.
        public bool IsVariant { get; }

        public static HierarchyBindingsLayer Resolve(string assetPath, bool isPrefabAsset)
        {
            if (isPrefabAsset == false)
                return new HierarchyBindingsLayer(assetPath, true, false, null);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (asset == null)
                throw new InvalidOperationException($"Prefab {assetPath} is not loadable.");

            if (PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant)
                return new HierarchyBindingsLayer(assetPath, false, false, null);

            // The chain of variants down to the first non-variant base: objects that came from any of its links
            // are "own" inherited ones. Everything else is the contents of nested prefabs.
            var chain = new HashSet<string>(StringComparer.Ordinal) { assetPath };
            var current = asset;

            while (PrefabUtility.GetPrefabAssetType(current) == PrefabAssetType.Variant)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(current);

                if (source == null)
                    throw new InvalidOperationException(
                        $"Variant {AssetDatabase.GetAssetPath(current)} has no base prefab.");

                current = source;
                chain.Add(AssetDatabase.GetAssetPath(current));
            }

            return new HierarchyBindingsLayer(assetPath, false, true, chain);
        }

        // An object is own if it or one of its ancestors is added as an override of this variant.
        // An added subtree has the flag on its root only, so we walk up.
        public bool IsOwn(GameObject target)
        {
            if (IsVariant == false)
                return true;

            for (var current = target.transform; current != null; current = current.parent)
            {
                if (PrefabUtility.IsAddedGameObjectOverride(current.gameObject))
                    return true;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(target) == false)
                throw new InvalidOperationException(
                    $"'{target.name}' is neither added by {AssetPath} nor inherited from its base. " +
                    "Layers are resolved only on loaded prefab contents.");

            return false;
        }

        public bool IsOwn(Component component)
        {
            if (IsVariant == false)
                return true;

            return IsOwn(component.gameObject) || PrefabUtility.IsAddedComponentOverride(component);
        }

        // An inherited object the variant added something inside becomes a pass-through node. If the object
        // itself came not from the chain of variants but from a nested prefab of the base, the edit of the
        // variant lies two instance levels deep - this case is not supported yet.
        public bool TryValidatePassThrough(GameObject target, string hierarchyPath, List<string> errors)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(target);

            if (source == null)
                throw new InvalidOperationException($"'{hierarchyPath}' is inherited but has no source object.");

            var sourcePath = AssetDatabase.GetAssetPath(source);

            if (_variantChain.Contains(sourcePath))
                return true;

            errors.Add(
                    $"'{hierarchyPath}' comes from the nested prefab {sourcePath}, and {AssetPath} adds content " +
                    "inside it. Variant layers inside nested prefabs are not supported yet: add the content to the " +
                    "nested prefab itself or give the object its own bindings there."
                );
            return false;
        }

        // The asset where the bindings component itself is declared. For an inherited component it is the base
        // (more precisely - the first link that added it), for an own one - the edited asset.
        public static string ResolveDeclaringAsset(Component bindings, string editedAssetPath)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(bindings) == false ||
                PrefabUtility.IsAddedComponentOverride(bindings))
                return editedAssetPath;

            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(bindings);

            if (source == null)
                throw new InvalidOperationException(
                    $"Bindings {bindings.GetType().Name} on '{bindings.name}' are inherited but have no source.");

            return AssetDatabase.GetAssetPath(source);
        }
    }
}
