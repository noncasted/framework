using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal
{
    // Regenerate All: the bindings of every prefab asset and of the loaded scenes.
    internal static class HierarchyBindingsProjectBatch
    {
        // The prefab assets of the project that carry bindings, their own or inherited.
        public static List<string> PrefabsWithBindings()
        {
            var paths = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (asset == null)
                    throw new InvalidOperationException($"{path} is not a prefab.");

                if (asset.GetComponentsInChildren<ObjectBindings>(true).Length > 0)
                    paths.Add(path);
            }

            return paths;
        }

        // Every binding declared in prefab assets and in loaded scenes, in one pass without phases (Regenerate
        // All). The bindings are already on the objects, so the "children before parents" order is not needed,
        // and all the code is written before one Refresh - otherwise after a failed compile a phase would wait
        // for a reload that never comes. Only bindings whose code changed (or whose object is stale) are bound
        // again. Scenes that are not loaded are not opened (opening scenes from tools crashes the shared
        // editor): they are listed in the log. The caller flushes once (changed = a file was written).
        public static int Prepare(out bool changed)
        {
            var items = new List<HierarchyBindingsPrefabItem>();

            foreach (var path in PrefabsWithBindings())
                HierarchyBindingsPrefabItems.CollectPrefab(path, items);

            if (HierarchyBindingsPrefabItems.ValidateUniqueTypes(items) == false)
                throw new InvalidOperationException("Duplicate bindings types. See the console.");

            var types = new HashSet<string>(items.Select(item => item.FullTypeName), StringComparer.Ordinal);
            var files = new HashSet<string>(items.Select(item => HierarchyBindingsPrefabItems.GeneratedFilePath(item.Request)),
                StringComparer.Ordinal);
            var prepared = 0;
            var failed = 0;
            changed = false;

            foreach (var item in items)
            {
                var request = item.Request;

                if (HierarchyBindingsGenerator.Prepare(HierarchyBindingsPrefabItems.FindInAsset(request), request.TypeName, request.Namespace,
                        request.IsSceneService, request.IsEntityComponent, out var fileChanged, false) == false)
                {
                    failed++;
                    continue;
                }

                prepared++;
                changed |= fileChanged;
            }

            var loadedScenes = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);

                if (scene.isLoaded == false)
                    continue;

                loadedScenes.Add(scene.path);

                foreach (var component in HierarchyBindingsSceneBatch.Collect(scene))
                {
                    // Bindings of prefab instances are regenerated in their prefabs above.
                    if (HierarchyBindingsLayer.ResolveDeclaringAsset(component, scene.path) != scene.path)
                        continue;

                    var type = HierarchyBindingsGenerator.ResolveGeneratedType(component);

                    if (type == null)
                        throw new InvalidOperationException(
                            $"{component.GetType().Name} in {scene.path} does not derive from a generated class.");

                    if (types.Add(type.FullName) == false)
                        throw new InvalidOperationException(
                            $"'{type.FullName}' in {scene.path} is used by more than one object.");

                    var file = HierarchyBindingsPaths.ToFilePath(
                        HierarchyBindingsPaths.ResolveGeneratedFolder(component.gameObject, scene.path), type.Name);

                    if (files.Add(file) == false)
                        throw new InvalidOperationException(
                            $"'{type.FullName}' in {scene.path} is generated into {file}, which another class uses.");

                    if (HierarchyBindingsGenerator.Prepare(component.gameObject, type.Name, type.Namespace ?? string.Empty,
                            component.IsSceneService, component.IsEntityComponent, out var fileChanged, false) == false)
                    {
                        failed++;
                        continue;
                    }

                    prepared++;
                    changed |= fileChanged;
                }
            }

            HierarchyBindingsSkippedScenes.Log(loadedScenes);

            if (failed > 0)
                throw new InvalidOperationException($"{failed} bindings failed to regenerate. See the console.");

            return prepared;
        }
    }
}
