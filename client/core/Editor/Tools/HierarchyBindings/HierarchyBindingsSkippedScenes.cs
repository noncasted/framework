using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Scenes that are not loaded are not opened by Regenerate All: the ones with bindings are named in the log.
    internal static class HierarchyBindingsSkippedScenes
    {
        private const string LogTag = HierarchyBindingsLog.Tag;

        public static void Log(HashSet<string> loadedScenes)
        {
            var scriptGuids = CollectBindingsScriptGuids();
            var skipped = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (loadedScenes.Contains(path) || ReferencesAny(path, scriptGuids) == false)
                    continue;

                skipped.Add(path);
            }

            if (skipped.Count == 0)
                return;

            Debug.Log(
                $"[{LogTag}] Not loaded scenes with bindings were skipped (open each and use Project Tools > " +
                $"Current Scene > Regenerate All Bindings): {string.Join(", ", skipped)}");
        }

        private static List<string> CollectBindingsScriptGuids()
        {
            var result = new List<string>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<ObjectBindings>())
            {
                foreach (var guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));

                    if (script != null && script.GetClass() == type)
                        result.Add(guid);
                }
            }

            return result;
        }

        private static bool ReferencesAny(string scenePath, List<string> scriptGuids)
        {
            var text = File.ReadAllText(CatalogPaths.ToFullPath(scenePath));

            foreach (var guid in scriptGuids)
            {
                if (text.Contains("guid: " + guid, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
