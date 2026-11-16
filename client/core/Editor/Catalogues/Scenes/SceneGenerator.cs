using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class SceneGenerator
    {
        private static IReadOnlyList<string> SearchFolders => FrameworkSettings.SceneFolders;

        // Like the other catalogues: nothing is written or imported while the domain loads, the run waits for the
        // first editor tick and for the end of an import or a compilation.
        private static readonly CatalogGenerationRunner Runner = new("SceneGenerator", Generate);

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return;

            // Build Settings scenes stay in the player, outside the content bundles, so their list is also a generation input.
            EditorBuildSettings.sceneListChanged += Runner.Schedule;
            Runner.RunDelayed();
        }

        // A scene added, deleted or moved in a scene folder (SceneCatalogPostprocessor).
        public static void ScheduleGenerate()
        {
            Runner.Schedule();
        }

        public static bool IsSceneInput(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) == false)
                return false;

            foreach (var folder in SearchFolders)
            {
                if (assetPath.StartsWith(folder + "/", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        [MenuItem(FrameworkMenu.Codegen + "Scenes", priority = FrameworkMenu.TrailingPriority)]
        public static void Generate()
        {
            Generate(SearchFolders, scenes =>
            {
                ScenesClassGenerator.Generate(scenes);
                ContentRootGenerator.ScheduleGenerate();
                Debug.Log($"[SceneGenerator] Found {scenes.Count} scene(s).");
            });
        }

        internal static void Generate(
            IReadOnlyList<string> searchFolders,
            Action<List<(string sceneName, string sceneGuid)>> write)
        {
            var folders = new List<string>();

            foreach (var folder in searchFolders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                    folders.Add(folder);
            }

            // No folder or no scene left is a catalogue too: an empty one, otherwise the members of the deleted
            // scenes stay in Scenes.cs.
            var guids = folders.Count > 0
                ? AssetDatabase.FindAssets("t:SceneAsset", folders.ToArray())
                : Array.Empty<string>();

            var scenes = new List<(string sceneName, string sceneGuid)>();
            var playerScenes = PlayerScenes();

            foreach (var guid in guids)
            {
                // A scene of the player (Startup) is loaded by the engine, not from a content bundle.
                if (playerScenes.Contains(guid))
                    continue;

                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(path);
                var sceneName = SanitizeSceneName(fileName);
                scenes.Add((sceneName, guid));
            }

            write(scenes);
        }

        // The GUIDs of the scenes of the Build Settings.
        private static HashSet<string> PlayerScenes()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                    result.Add(scene.guid.ToString());
            }

            return result;
        }

        private static string SanitizeSceneName(string fileName)
        {
            var sb = new StringBuilder(fileName.Length);
            var capitalizeNext = true;

            foreach (var c in fileName)
            {
                if (c == '_' || c == '-' || c == ' ')
                {
                    capitalizeNext = true;
                    continue;
                }

                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(capitalizeNext ? char.ToUpper(c) : c);
                    capitalizeNext = false;
                }
            }

            return sb.ToString();
        }
    }
}