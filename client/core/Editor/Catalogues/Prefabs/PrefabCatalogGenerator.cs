using Unity.Scripting.LifecycleManagement;
using UnityEditor;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class PrefabCatalogGenerator
    {
        public static string GroupsFolder => FrameworkSettings.Content("Runtime/Catalogues/Prefabs");


        private static readonly CatalogGenerationRunner Runner = new("PrefabCatalogGenerator", GenerateInternal);
        private static readonly ILog _log = Logs.Of(typeof(PrefabCatalogGenerator));

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            Runner.RunDelayed();
        }

        [MenuItem(FrameworkMenu.Codegen + "Prefabs Catalog", priority = FrameworkMenu.TrailingPriority)]
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
            PrefabRootResolver.Reset();

            var groups = PrefabGroupBuilder.BuildGroups(PrefabSourceCollector.CollectSources());

            AssetDatabase.StartAssetEditing();

            try
            {
                PrefabGroupAssetWriter.WriteGroupAssets(groups, GroupsFolder);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            PrefabRootResolver.ApplyMetadataFixes();

            // The ContentRoot lists the group assets: one written, renamed or deleted changes it.
            ContentRootGenerator.ScheduleGenerate();
            PrefabsCatalogClassGenerator.Generate(groups);
            PrefabGroupsRegistry.Instance.Invalidate();
            AssetDatabase.SaveAssets();

            _log.Info("Generated {Groups} prefab group(s).", groups.Count);
        }
    }
}
