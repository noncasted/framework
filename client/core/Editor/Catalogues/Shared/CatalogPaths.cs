using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    public static class CatalogPaths
    {
        public static string ToFullPath(string assetPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, assetPath);
        }

        public static string ToAssetPath(string fullPath)
        {
            return ToAssetPath(Path.GetDirectoryName(Application.dataPath), fullPath);
        }

        public static string ToAssetPath(string projectRoot, string fullPath)
        {
            var normalized = Path.GetFullPath(fullPath).Replace('\\', '/');
            var root = projectRoot.Replace('\\', '/');

            if (normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase) == false)
                return normalized;

            return normalized.Substring(root.Length).TrimStart('/');
        }

        // The importer of a source the scan found on disk. A path Unity does not import (a hidden or `~` folder) has none
        // and is no source: null. Any other file without one is not imported yet or broken: taken for "not marked", its
        // group would lose it and the stale cleanup would delete the group asset and its class.
        public static AssetImporter SourceImporter(string assetPath)
        {
            if (IsIgnoredByUnity(assetPath))
                return null;

            return AssetImporter.GetAtPath(assetPath)
                   ?? throw new InvalidOperationException($"{assetPath} has no importer: it is not imported, the catalogue is not generated");
        }

        // The rules of Unity for the files of Assets it skips: hidden, `~` folders, cvs and .tmp.
        public static bool IsIgnoredByUnity(string assetPath)
        {
            foreach (var segment in assetPath.Split('/'))
            {
                if (segment.StartsWith(".", StringComparison.Ordinal)
                    || segment.EndsWith("~", StringComparison.Ordinal)
                    || segment.Equals("cvs", StringComparison.OrdinalIgnoreCase)
                    || segment.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts = folderPath.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];

                if (AssetDatabase.IsValidFolder(next) == false)
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}