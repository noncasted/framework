using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal
{
    public static class HierarchyBindingsMenu
    {
        private const string HierarchyPath = "GameObject/Create Object Bindings";
        private const string ToolsPath = FrameworkMenu.Root + "Create Object Bindings";
        private const string ValidateScenesPath = FrameworkMenu.ObjectBindings + "Validate Open Scenes";
        private const string CancelBatchPath = FrameworkMenu.ObjectBindings + "Cancel Prefab Batch";
        private const string RegeneratePrefabsPath = "Assets/Object Bindings/Regenerate Prefabs";
        private const string ValidatePrefabsPath = "Assets/Object Bindings/Validate Prefabs";
        private const string LogTag = "HierarchyBindingsGenerator";

        // The validator must be parameterless, otherwise Unity does not register the item at all.
        // We work from Selection, not MenuCommand: this way the item is invoked once,
        // not once per selected object.
        [MenuItem(HierarchyPath, false, 30)]
        private static void OpenFromHierarchy()
        {
            Open();
        }

        [MenuItem(HierarchyPath, true)]
        private static bool ValidateHierarchy()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem(ToolsPath, false, FrameworkMenu.Priority)]
        private static void OpenFromTools()
        {
            Open();
        }

        [MenuItem(ToolsPath, true)]
        private static bool ValidateTools()
        {
            return Selection.activeGameObject != null;
        }

        // The selected prefabs and every prefab in the selected folders.
        [MenuItem(RegeneratePrefabsPath, false, 1000)]
        private static void RegeneratePrefabs()
        {
            Debug.Log($"[{LogTag}] {HierarchyBindingsBatch.RegeneratePrefabs(SelectedPrefabs())}");
        }

        [MenuItem(RegeneratePrefabsPath, true)]
        private static bool ValidateRegeneratePrefabs()
        {
            return HierarchyBindingsBatch.IsRunning == false && Selection.assetGUIDs.Length > 0;
        }

        [MenuItem(ValidatePrefabsPath, false, 1001)]
        private static void ValidatePrefabs()
        {
            var paths = SelectedPrefabs();
            var errors = HierarchyBindingsValidator.ValidatePrefabs(paths);
            Report(errors, $"{paths.Count} prefabs");
        }

        [MenuItem(ValidatePrefabsPath, true)]
        private static bool ValidateValidatePrefabs()
        {
            return Selection.assetGUIDs.Length > 0;
        }

        [MenuItem(ValidateScenesPath, false, FrameworkMenu.GroupPriority)]
        private static void ValidateOpenScenes()
        {
            var errors = 0;

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);

                if (scene.isLoaded)
                    errors += HierarchyBindingsValidator.ValidateScene(scene);
            }

            Report(errors, $"{SceneManager.sceneCount} open scenes");
        }

        [MenuItem(CancelBatchPath, false, FrameworkMenu.GroupPriority + 1)]
        private static void CancelBatch()
        {
            HierarchyBindingsBatch.Cancel();
        }

        [MenuItem(CancelBatchPath, true)]
        private static bool ValidateCancelBatch()
        {
            return HierarchyBindingsBatch.IsRunning;
        }

        private static void Report(int errors, string scope)
        {
            var message = $"[{LogTag}] Validated {scope}: {errors} errors.";

            if (errors > 0)
                Debug.LogError(message);
            else
                Debug.Log(message);
        }

        private static List<string> SelectedPrefabs()
        {
            var result = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var guid in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var prefabGuid in AssetDatabase.FindAssets("t:Prefab", new[] { path }))
                        result.Add(AssetDatabase.GUIDToAssetPath(prefabGuid));

                    continue;
                }

                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    result.Add(path);
            }

            return result.ToList();
        }

        private static void Open()
        {
            var target = Selection.activeGameObject;

            if (target == null)
            {
                Debug.LogWarning("[HierarchyBindingsGenerator] Select a GameObject first.");
                return;
            }

            HierarchyBindingsWindow.Open(target);
        }
    }
}