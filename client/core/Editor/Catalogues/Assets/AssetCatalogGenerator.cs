using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class AssetCatalogGenerator
    {
        public static string GroupsFolder => FrameworkSettings.Content("Runtime/Catalogues/Assets");

        private const string GroupPrefix = "Assets_";
        private const string LogTag = "AssetCatalogGenerator";

        private static readonly CatalogGenerationRunner Runner = new(LogTag, GenerateInternal);

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            Runner.RunDelayed();
        }

        [MenuItem(FrameworkMenu.Codegen + "Assets Catalog", priority = FrameworkMenu.TrailingPriority)]
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

        private static void GenerateInternal()
        {
            var groups = AssetGroupBuilder.BuildGroups(AssetSourceCollector.CollectSources());

            // Moving between Addressables and Resources happens before batch editing: inside it
            // a moved asset cannot be read by its new path yet, and the group would be created again.
            AssetGroupAssetWriter.MoveGroupAssets(groups, GroupsFolder);

            AssetDatabase.StartAssetEditing();

            try
            {
                AssetGroupAssetWriter.WriteGroupAssets(groups, GroupsFolder);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Resources groups do not go into Addressables: sync would delete their addressable groups as extra.
            CatalogAddressablesSync.Sync(LogTag, GroupPrefix, GroupsFolder, groups.FindAll(group => group.IsAddressable));
            AssetsCatalogClassGenerator.Generate(groups);
            AssetGroupsRegistry.Instance.Invalidate();
            AssetDatabase.SaveAssets();

            Debug.Log($"[{LogTag}] Generated {groups.Count} asset group(s).");
        }
    }
}
