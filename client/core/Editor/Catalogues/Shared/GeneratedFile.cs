using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Writing generated scripts: write only when the content changed and clean up what
    // the generator did not produce in this run.
    public static class GeneratedFile
    {
        private static readonly ILog _log = Logs.Of(typeof(GeneratedFile));

        // Returns true if the file was written. A failed read or write throws: a caller that waits for the
        // recompilation of what it wrote would otherwise wait for a reload that never comes.
        public static bool WriteIfChanged(string assetPath, string content)
        {
            var fullPath = CatalogPaths.ToFullPath(assetPath);

            if (File.Exists(fullPath) && File.ReadAllText(fullPath) == content)
                return false;

            var directory = Path.GetDirectoryName(fullPath);

            if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
                Directory.CreateDirectory(directory);

            File.WriteAllText(fullPath, content);
            AssetDatabase.ImportAsset(assetPath);
            _log.Debug("Generated {Path}", assetPath);
            return true;
        }

        // Several generators share the Generated folder, so foreign files are filtered out by the class name
        // suffix: the prefab catalog touches only *Prefabs.cs, the asset catalog only *Assets.cs.
        public static void DeleteStale(string folder, HashSet<string> writtenNames, string ownedSuffix)
        {
            foreach (var file in EnumerateScripts(folder))
            {
                var fileName = Path.GetFileName(file);

                if (writtenNames.Contains(fileName) || IsOwned(fileName, ownedSuffix) == false)
                    continue;

                Delete($"{folder}/{fileName}", file);
            }
        }

        // The groups by the folder of their class; a group without codegen has none.
        internal static Dictionary<string, List<TGroup>> GroupByFolder<TGroup>(
            string logTag,
            IReadOnlyList<TGroup> groups,
            Func<TGroup, bool> skip,
            Func<TGroup, string> getFolder)
            where TGroup : ICatalogGroupDefinition
        {
            var byFolder = new Dictionary<string, List<TGroup>>(StringComparer.Ordinal);

            foreach (var group in groups)
            {
                if (skip(group))
                    continue;

                var folder = getFolder(group);

                if (string.IsNullOrEmpty(folder))
                    throw new InvalidOperationException($"[{logTag}] Group '{group.Name}' has no generated folder");

                if (byFolder.TryGetValue(folder, out var list) == false)
                {
                    list = new List<TGroup>();
                    byFolder.Add(folder, list);
                }

                list.Add(group);
            }

            return byFolder;
        }

        // The classes of this generator in every other Generated folder of the project.
        public static void DeleteFromUnusedFolders(string internalFolder, string ownedSuffix, HashSet<string> writtenFolders)
        {
            if (writtenFolders.Contains(internalFolder) == false)
                DeleteFolderScripts(internalFolder, ownedSuffix);

            var assetsRoot = Application.dataPath;

            if (Directory.Exists(assetsRoot) == false)
                return;

            // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
            var folders = Directory.GetDirectories(assetsRoot, "Generated", SearchOption.AllDirectories);
            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var directory in folders)
            {
                var folder = CatalogPaths.ToAssetPath(projectRoot, directory);

                if (writtenFolders.Contains(folder))
                    continue;

                DeleteFolderScripts(folder, ownedSuffix);
            }
        }

        public static void DeleteFolderScripts(string folder, string ownedSuffix)
        {
            foreach (var file in EnumerateScripts(folder))
            {
                var fileName = Path.GetFileName(file);

                if (IsOwned(fileName, ownedSuffix) == false)
                    continue;

                Delete($"{folder}/{fileName}", file);
            }
        }

        private static bool IsOwned(string fileName, string ownedSuffix)
        {
            return fileName.EndsWith(ownedSuffix, StringComparison.Ordinal);
        }

        private static IReadOnlyList<string> EnumerateScripts(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder) == false)
                return Array.Empty<string>();

            return Directory.GetFiles(CatalogPaths.ToFullPath(folder), "*.cs", SearchOption.TopDirectoryOnly);
        }

        private static void Delete(string assetPath, string fullPath)
        {
            if (AssetDatabase.DeleteAsset(assetPath) == false)
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception exception)
                {
                    _log.Error(exception, "Failed to delete {Path}", assetPath);
                    return;
                }
            }

            _log.Info("Deleted stale file: {Path}", assetPath);
        }
    }
}