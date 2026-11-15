using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class AudioCatalogGenerator
    {
        public static string GroupsFolder => FrameworkSettings.Content("Runtime/Catalogues/Audio");

        private const string GroupPrefix = "Audio_";
        private const string LogTag = "AudioCatalogGenerator";

        private static readonly CatalogGenerationRunner Runner = new(LogTag, GenerateInternal);

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            Runner.RunDelayed();
        }

        [MenuItem(FrameworkMenu.Codegen + "Audio Catalog", priority = FrameworkMenu.TrailingPriority)]
        public static void Generate()
        {
            Runner.Run();
        }

        public static void GenerateNow()
        {
            Runner.RunNow();
        }

        public static void ScheduleGenerate()
        {
            Runner.Schedule();
        }

        // Only the volume of already generated entries: group contents and code are not touched in Play mode.
        public static void SyncVolumes(IReadOnlyList<AssetImporter> importers)
        {
            foreach (var importer in importers)
            {
                if (AudioCatalogMetadata.TryRead(importer, out var metadata) == false || metadata.Included == false)
                    continue;

                var groupName = CatalogNaming.ToGroupName(metadata.Group);
                var asset = AssetDatabase.LoadAssetAtPath<AudioGroupAsset>($"{GroupsFolder}/{groupName}.asset");

                if (asset == null)
                    continue;

                var name = CatalogNaming.ToGroupName(AudioSourcePaths.GetSourceName(importer.assetPath));

                if (asset.TrySetVolume(name, Mathf.Clamp01(metadata.Volume)))
                    EditorUtility.SetDirty(asset);
            }
        }
        public static bool IsSourcePath(string path)
        {
            return AudioSourcePaths.IsSourcePath(path);
        }

        public static bool IsEncodedPath(string path)
        {
            return AudioSourcePaths.IsEncodedPath(path);
        }

        private static void GenerateInternal()
        {
            // Missing settings stop the run before anything is written.
            CatalogAddressablesSync.RequireSettings();

            var groups = AudioGroupBuilder.BuildGroups(AudioSourceCollector.CollectSources());

            AssetDatabase.StartAssetEditing();

            try
            {
                AudioGroupAssetWriter.WriteGroupAssets(groups, GroupsFolder);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            CatalogAddressablesSync.Sync(LogTag, GroupPrefix, GroupsFolder, groups);
            AudioCatalogClassGenerator.Generate(groups);
            AudioGroupsRegistry.Instance.Invalidate();
            AssetDatabase.SaveAssets();

            Debug.Log($"[{LogTag}] Generated {groups.Count} audio group(s).");
        }
    }
}
