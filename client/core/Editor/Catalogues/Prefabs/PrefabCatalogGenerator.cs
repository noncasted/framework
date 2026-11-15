using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class PrefabCatalogGenerator
    {
        public static string GroupsFolder => FrameworkSettings.Content("Runtime/Catalogues/Prefabs");

        private const string GroupPrefix = "Prefabs_";

        private static readonly CatalogGenerationRunner Runner = new("PrefabCatalogGenerator", GenerateInternal);

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

            // Missing settings stop the run before anything is written.
            CatalogAddressablesSync.RequireSettings();

            var groups = PrefabGroupBuilder.BuildGroups(PrefabSourceCollector.CollectSources());

            // Moving between Addressables and Resources happens before batch editing: inside it
            // a moved asset cannot be read by its new path yet, and the group would be created again.
            PrefabGroupAssetWriter.MoveGroupAssets(groups, GroupsFolder);

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

            // Resources groups do not go into Addressables: sync would delete their addressable groups as extra.
            CatalogAddressablesSync.Sync("PrefabCatalogGenerator", GroupPrefix, GroupsFolder,
                groups.FindAll(group => group.IsAddressable));
            PrefabsCatalogClassGenerator.Generate(groups);
            PrefabGroupsRegistry.Instance.Invalidate();
            AssetDatabase.SaveAssets();

            Debug.Log($"[PrefabCatalogGenerator] Generated {groups.Count} prefab group(s).");
        }
    }
}
