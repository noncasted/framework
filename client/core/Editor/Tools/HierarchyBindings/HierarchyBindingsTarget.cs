using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal
{
    // A domain reload happens between writing the code and filling the references, so the object
    // has to be described with strings and found again. A prefab is opened as contents:
    // this way the edit goes into the asset itself, not into an override in the open scene.
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
                if (TryBuildPath(target.transform, stage.prefabContentsRoot.transform, out objectPath) == false)
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
                return TryBuildPath(target.transform, root, out objectPath);
            }

            if (target.scene.IsValid() == false || string.IsNullOrEmpty(target.scene.path))
            {
                error = "Object is not part of a saved scene or prefab.";
                return false;
            }

            assetPath = target.scene.path;
            return TryBuildPath(target.transform, null, out objectPath);
        }

        public static HierarchyBindingsTarget Resolve(HierarchyBindingsRequest request, out string error)
        {
            error = string.Empty;

            if (request.IsPrefabAsset)
                return ResolvePrefab(request.AssetPath, request.ObjectPath, out error);

            return ResolveScene(request, out error);
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
                return ResolvePrefab(assetPath, objectPath, out error);

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

        private static HierarchyBindingsTarget ResolvePrefab(string assetPath, string objectPath, out string error)
        {
            error = string.Empty;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            if (stage != null && stage.assetPath == assetPath)
            {
                var staged = FindChild(stage.prefabContentsRoot.transform, objectPath);

                if (staged == null)
                {
                    error = $"'{objectPath}' is missing in the open prefab stage.";
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

            var found = FindChild(contents.transform, objectPath);

            if (found == null)
            {
                PrefabUtility.UnloadPrefabContents(contents);
                error = $"'{objectPath}' is missing in {assetPath}.";
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
            var rootName = separator < 0 ? request.ObjectPath : request.ObjectPath.Substring(0, separator);
            var rest = separator < 0 ? string.Empty : request.ObjectPath.Substring(separator + 1);

            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                if (sceneRoot.name != rootName)
                    continue;

                var found = FindChild(sceneRoot.transform, rest);

                if (found == null)
                    continue;

                return new HierarchyBindingsTarget
                {
                    Root = found.gameObject,
                    _assetPath = request.AssetPath,
                    _scene = scene
                };
            }

            error = $"'{request.ObjectPath}' is missing in {request.AssetPath}.";
            return null;
        }

        // The path is built from the given root; for a scene there is no root and the path goes to the very top.
        private static bool TryBuildPath(Transform target, Transform root, out string path)
        {
            path = string.Empty;

            var names = new List<string>();
            var current = target;

            while (current != null)
            {
                if (current == root)
                {
                    names.Reverse();
                    path = string.Join("/", names);
                    return true;
                }

                names.Add(current.name);
                current = current.parent;
            }

            if (root != null)
                return false;

            names.Reverse();
            path = string.Join("/", names);
            return true;
        }

        private static Transform FindChild(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;

            return root.Find(path);
        }
    }
}