using System;
using System.Collections.Generic;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public sealed class ContainerGraphPostprocessor : AssetPostprocessor
    {
        private static readonly CatalogGenerationRunner Runner = new("ContainerGraph", GenerateInternal);
        private static readonly ILog _log = Logs.Of<ContainerGraphPostprocessor>();

        // Short names of the holder and component types of the last written map (the map itself is compiled into the
        // content assembly of the project, which this assembly does not reference). Filled on every generation, and
        // the map is generated after every domain reload.
        private static readonly HashSet<string> MappedTypes = new(StringComparer.Ordinal);

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            Runner.RunDelayed();
        }

        [MenuItem(FrameworkMenu.Codegen + "Container Asset Map", priority = FrameworkMenu.TrailingPriority)]
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

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsTarget(importedAssets) ||
                ContainsTarget(deletedAssets) ||
                ContainsTarget(movedAssets) ||
                ContainsTarget(movedFromAssetPaths) ||
                ContainsMappedScript(importedAssets) ||
                ContainsMappedScript(deletedAssets) ||
                ContainsMappedScript(movedFromAssetPaths))
                ScheduleGenerate();
        }

        private static void GenerateInternal()
        {
            var assetsRoot = Application.dataPath;
            var assets = ContainerGraphAssetScanner.Scan(assetsRoot);
            ContainerGraphAssetWriter.Write(assets);
            CollectMappedTypes(assets);
            _log.Info("Wrote {Entries} asset graph entries.", assets.Count);
        }

        private static void CollectMappedTypes(IReadOnlyList<ContainerGraphAsset> assets)
        {
            MappedTypes.Clear();

            for (var i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                MappedTypes.Add(ShortName(asset.HolderType));

                for (var t = 0; t < asset.ComponentTypes.Length; t++)
                    MappedTypes.Add(ShortName(asset.ComponentTypes[t]));
            }
        }

        // A namespace or class rename does not touch the prefabs, but makes the map stale, and the build fails with
        // CINGR003 on it. So the map is regenerated on an edit of a script it lists too.
        private static bool ContainsMappedScript(string[] paths)
        {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];

                if (string.IsNullOrEmpty(path) ||
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                // XBindings.g.cs -> XBindings
                var name = Path.GetFileName(path);
                name = name.Substring(0, name.IndexOf('.'));

                if (MappedTypes.Contains(name))
                    return true;
            }

            return false;
        }

        private static string ShortName(string type)
        {
            var dot = type.LastIndexOf('.');
            return dot >= 0 ? type.Substring(dot + 1) : type;
        }

        private static bool ContainsTarget(string[] paths)
        {
            if (paths == null)
                return false;

            for (var i = 0; i < paths.Length; i++)
            {
                if (IsTarget(paths[i]))
                    return true;
            }

            return false;
        }

        private static bool IsTarget(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var extension = Path.GetExtension(path);

            return extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".unity", StringComparison.OrdinalIgnoreCase);
        }
    }
}