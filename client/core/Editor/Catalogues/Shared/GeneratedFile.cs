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
        public static void WriteIfChanged(string logTag, string assetPath, string content)
        {
            try
            {
                var fullPath = CatalogPaths.ToFullPath(assetPath);

                if (File.Exists(fullPath) && File.ReadAllText(fullPath) == content)
                    return;

                var directory = Path.GetDirectoryName(fullPath);

                if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
                    Directory.CreateDirectory(directory);

                File.WriteAllText(fullPath, content);
                AssetDatabase.ImportAsset(assetPath);
                Debug.Log($"[{logTag}] Generated {assetPath}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{logTag}] Failed to write {assetPath}: {exception}");
            }
        }

        // Several generators share the Generated folder, so foreign files are filtered out by the class name
        // suffix: the prefab catalog touches only *Prefabs.cs, the asset catalog only *Assets.cs.
        public static void DeleteStale(string logTag, string folder, HashSet<string> writtenNames, string ownedSuffix)
        {
            foreach (var file in EnumerateScripts(logTag, folder))
            {
                var fileName = Path.GetFileName(file);

                if (writtenNames.Contains(fileName) || IsOwned(fileName, ownedSuffix) == false)
                    continue;

                Delete(logTag, $"{folder}/{fileName}", file);
            }
        }

        public static void DeleteFolderScripts(string logTag, string folder, string ownedSuffix)
        {
            foreach (var file in EnumerateScripts(logTag, folder))
            {
                var fileName = Path.GetFileName(file);

                if (IsOwned(fileName, ownedSuffix) == false)
                    continue;

                Delete(logTag, $"{folder}/{fileName}", file);
            }
        }

        private static bool IsOwned(string fileName, string ownedSuffix)
        {
            return fileName.EndsWith(ownedSuffix, StringComparison.Ordinal);
        }

        private static IReadOnlyList<string> EnumerateScripts(string logTag, string folder)
        {
            if (AssetDatabase.IsValidFolder(folder) == false)
                return Array.Empty<string>();

            try
            {
                return Directory.GetFiles(CatalogPaths.ToFullPath(folder), "*.cs", SearchOption.TopDirectoryOnly);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{logTag}] Failed to scan {folder}: {exception}");
                return Array.Empty<string>();
            }
        }

        private static void Delete(string logTag, string assetPath, string fullPath)
        {
            if (AssetDatabase.DeleteAsset(assetPath) == false)
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[{logTag}] Failed to delete {assetPath}: {exception}");
                    return;
                }
            }

            Debug.Log($"[{logTag}] Deleted stale file: {assetPath}");
        }
    }
}