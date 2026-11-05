using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class SpriteGenerator
    {
        public static string GroupsFolder => FrameworkSettings.Content("Runtime/Catalogues/Sprites");

        private const string GroupPrefix = "Sprites_";

        private static readonly CatalogGenerationRunner Runner = new("SpriteGenerator", GenerateInternal);

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            Runner.RunDelayed();
        }

        [MenuItem(FrameworkMenu.Codegen + "Sprites", priority = FrameworkMenu.TrailingPriority)]
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
            if (SpriteArtRoots.HasExistingFolder == false)
                return;

            var groups = new List<SpriteGroupDefinition>();
            var sources = SpriteSourceCollector.CollectSources();

            // Baking happens outside StartAssetEditing: new sprites must become assets right away
            // so groups can reference them.
            BakeAnimations(sources);

            AssetDatabase.StartAssetEditing();

            try
            {
                groups = SpriteGroupBuilder.BuildGroups(sources);
                SpriteGroupAssetWriter.WriteGroupAssets(groups, GroupsFolder);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            CatalogAddressablesSync.Sync("SpriteGenerator", GroupPrefix, GroupsFolder, groups);
            SpritesClassGenerator.Generate(groups);
            SpriteGroupsRegistry.Instance.Invalidate();
            AssetDatabase.SaveAssets();

            Debug.Log($"[SpriteGenerator] Generated {groups.Count} sprite group(s).");
        }

        private static void BakeAnimations(List<SpriteSource> sources)
        {
            var writtenPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var source in sources)
            {
                if (source.IsMultipleAnimations == false)
                    continue;

                source.SetBakedAnimations(AsepriteAnimationBaker.Bake(
                    source.Path,
                    CatalogNaming.ToGroupKey(source.Group),
                    source.PropertyName,
                    source.Metadata,
                    writtenPaths));
            }

            AsepriteAnimationBaker.DeleteStale(writtenPaths);
        }
    }
}
