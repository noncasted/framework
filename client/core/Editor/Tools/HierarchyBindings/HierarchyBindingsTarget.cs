using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal
{
    // A domain reload happens between writing the code and filling the references, so the object
    // has to be described with strings and found again. A prefab is opened as contents:
    // this way the edit goes into the asset itself, not into an override in the open scene.
    // The object is found by sibling indices: names repeat among siblings (two Panel children) and may contain '/',
    // which Transform.Find takes for a separator.
    internal sealed class HierarchyBindingsTarget : IDisposable
    {
        public GameObject Root { get; private set; }

        private GameObject _loadedContents;
        private string _assetPath;
        private Scene _scene;

        public static bool TryDescribe(
            GameObject target,
            out string assetPath,
            out string objectPath,
            out bool isPrefabAsset,
            out string error)
        {
            assetPath = string.Empty;
            objectPath = string.Empty;
            isPrefabAsset = false;
            error = string.Empty;

            var stage = PrefabStageUtility.GetPrefabStage(target);

            if (stage != null)
            {
                if (TryBuildIndexPath(target.transform, stage.prefabContentsRoot.transform, out objectPath) == false)
                {
                    error = "Object is outside the prefab being edited.";
                    return false;
                }

                assetPath = stage.assetPath;
                isPrefabAsset = true;
                return true;
            }

            if (PrefabUtility.IsPartOfPrefabAsset(target))
            {
                var root = target.transform.root;
                assetPath = AssetDatabase.GetAssetPath(root.gameObject);
                isPrefabAsset = true;
                return TryBuildIndexPath(target.transform, root, out objectPath);
            }

            if (target.scene.IsValid() == false || string.IsNullOrEmpty(target.scene.path))
            {
                error = "Object is not part of a saved scene or prefab.";
                return false;
            }

            assetPath = target.scene.path;
            return TryBuildIndexPath(target.transform, null, out objectPath);
        }

        public static HierarchyBindingsTarget Resolve(HierarchyBindingsRequest request, out string error)
        {
            error = string.Empty;

            if (request.IsPrefabAsset)
                return ResolvePrefab(request.AssetPath, request.ObjectPath, request.ObjectNames, out error);

            return ResolveScene(request, out error);
        }

        // The names from the top of the hierarchy, for messages.
        public static string NamePath(GameObject target)
        {
            var names = new List<string>();

            for (var current = target.transform; current != null; current = current.parent)
                names.Add(current.name);

            names.Reverse();
            return string.Join("/", names);
        }

        // Sibling indices from the root (excluded) down to the target.
        public static string IndexPath(Transform target, Transform root)
        {
            if (TryBuildIndexPath(target, root, out var path) == false)
                throw new InvalidOperationException($"'{target.name}' is not under '{root.name}'.");

            return path;
        }

        public static Transform FindByIndexPath(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;

            var current = root;

            foreach (var segment in path.Split('/'))
            {
                var index = int.Parse(segment, CultureInfo.InvariantCulture);

                if (index >= current.childCount)
                    return null;

                current = current.GetChild(index);
            }

            return current;
        }

        // The target to scan in the code generation phase. An object in a prefab stage or in a scene is
        // scanned as is, and an object selected right in the asset - through the loaded contents: the
        // variant layers are resolved only there (see HierarchyBindingsLayer), and the binding after the
        // domain reload looks there too, so the code and the references are built from the same tree.
        public static HierarchyBindingsTarget OpenForScan(
            GameObject target,
            string assetPath,
            string objectPath,
            bool isPrefabAsset,
            out string error)
        {
            error = string.Empty;

            if (isPrefabAsset && PrefabUtility.IsPartOfPrefabAsset(target))
                return ResolvePrefab(assetPath, objectPath, NamePath(target), out error);

            return new HierarchyBindingsTarget
            {
                Root = target,
                _assetPath = assetPath
            };
        }

        public void Commit()
        {
            if (_loadedContents != null)
            {
                PrefabUtility.SaveAsPrefabAsset(_loadedContents, _assetPath);
                return;
            }

            if (_scene.IsValid())
                EditorSceneManager.MarkSceneDirty(_scene);
        }

        public void Dispose()
        {
            if (_loadedContents == null)
                return;

            PrefabUtility.UnloadPrefabContents(_loadedContents);
            _loadedContents = null;
        }

        private static HierarchyBindingsTarget ResolvePrefab(
            string assetPath,
            string objectPath,
            string objectNames,
            out string error)
        {
            error = string.Empty;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            if (stage != null && stage.assetPath == assetPath)
            {
                var staged = FindByIndexPath(stage.prefabContentsRoot.transform, objectPath);

                if (staged == null)
                {
                    error = $"'{objectNames}' is missing in the open prefab stage.";
                    return null;
                }

                // Unity saves the open stage scene itself, we only need to mark it dirty.
                return new HierarchyBindingsTarget
                {
                    Root = staged.gameObject,
                    _assetPath = assetPath,
                    _scene = stage.scene
                };
            }

            var contents = PrefabUtility.LoadPrefabContents(assetPath);

            if (contents == null)
            {
                error = $"Failed to open prefab {assetPath}.";
                return null;
            }

            var found = FindByIndexPath(contents.transform, objectPath);

            if (found == null)
            {
                PrefabUtility.UnloadPrefabContents(contents);
                error = $"'{objectNames}' is missing in {assetPath}.";
                return null;
            }

            return new HierarchyBindingsTarget
            {
                Root = found.gameObject,
                _assetPath = assetPath,
                _loadedContents = contents
            };
        }

        private static HierarchyBindingsTarget ResolveScene(HierarchyBindingsRequest request, out string error)
        {
            error = string.Empty;

            var scene = SceneManager.GetSceneByPath(request.AssetPath);

            if (scene.IsValid() == false || scene.isLoaded == false)
            {
                error = $"Scene {request.AssetPath} is not open.";
                return null;
            }

            var separator = request.ObjectPath.IndexOf('/');
            var rootIndex = int.Parse(separator < 0 ? request.ObjectPath : request.ObjectPath.Substring(0, separator),
                CultureInfo.InvariantCulture);
            var rest = separator < 0 ? string.Empty : request.ObjectPath.Substring(separator + 1);
            var roots = scene.GetRootGameObjects();
            var found = rootIndex < roots.Length ? FindByIndexPath(roots[rootIndex].transform, rest) : null;

            if (found == null)
            {
                error = $"'{request.ObjectNames}' is missing in {request.AssetPath}.";
                return null;
            }

            return new HierarchyBindingsTarget
            {
                Root = found.gameObject,
                _assetPath = request.AssetPath,
                _scene = scene
            };
        }

        // The path is built from the given root; for a scene there is no root and the path goes to the very top,
        // where the sibling index of a root object is its index among the roots of the scene.
        private static bool TryBuildIndexPath(Transform target, Transform root, out string path)
        {
            path = string.Empty;

            var indices = new List<string>();
            var current = target;

            while (current != null)
            {
                if (current == root)
                {
                    indices.Reverse();
                    path = string.Join("/", indices);
                    return true;
                }

                indices.Add(current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture));
                current = current.parent;
            }

            if (root != null)
                return false;

            indices.Reverse();
            path = string.Join("/", indices);
            return true;
        }
    }
}