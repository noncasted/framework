using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class AssetCatalogGenerator
    {
        public static string GroupsFolder => FrameworkSettings.Content("Runtime/Catalogues/Assets");

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

            AssetDatabase.StartAssetEditing();

            try
            {
                AssetGroupAssetWriter.WriteGroupAssets(groups, GroupsFolder);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // The ContentRoot lists the group assets: one written, renamed or deleted changes it.
            ContentRootGenerator.ScheduleGenerate();
            AssetsCatalogClassGenerator.Generate(groups);
            AssetGroupsRegistry.Instance.Invalidate();
            AssetDatabase.SaveAssets();

            Debug.Log($"[{LogTag}] Generated {groups.Count} asset group(s).");
        }
    }
}
