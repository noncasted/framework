using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // The root component of a prefab, stored in its markup as a script GUID with a readable name beside it.
    [NoAutoStaticsCleanup]
    internal static class PrefabRootResolver
    {
        private static readonly ILog _log = Logs.Of(typeof(PrefabRootResolver));

        private static readonly List<MetadataFix> _metadataFixes = new();

        internal static void Reset()
        {
            _metadataFixes.Clear();
        }

        // userData cannot hold a live type reference, so the root component is stored as a script GUID
        // with a readable name beside it. The GUID survives renames; the name is re-derived every run.
        internal static string ResolveComponentType(string path, GameObject prefab, PrefabCatalogMetadata metadata)
        {
            var componentType = metadata.ComponentType ?? string.Empty;
            var componentGuid = metadata.ComponentGuid ?? string.Empty;

            if (string.IsNullOrEmpty(componentType) && string.IsNullOrEmpty(componentGuid))
                return string.Empty;

            var resolved = PrefabCatalogMetadata.ResolveType(componentGuid, componentType);
            var behaviour = FindBehaviour(prefab, resolved, componentType);

            // Falling back to GameObject would change the type of the catalogue property and break its users.
            if (behaviour == null)
                throw new InvalidOperationException(
                    $"[PrefabCatalogGenerator] Root type '{PrefabCatalogMetadata.ToDisplayName(componentType)}' is " +
                    $"missing on {path}. Reassign the root in the inspector.");

            // The previous domain is still loaded after a failed compile: the code gets the names from the sources,
            // the metadata keeps the old names until the next clean run fixes them.
            if (ScriptTypeNames.IsDomainStale)
            {
                var script = MonoScript.FromMonoBehaviour(behaviour);
                var type = behaviour.GetType();
                return ScriptTypeNames.FullName(type, script) + ", " + ScriptTypeNames.AssemblyName(type, script);
            }

            var actualType = behaviour.GetType().AssemblyQualifiedName ?? string.Empty;
            var actualGuid = PrefabCatalogMetadata.ToScriptGuid(behaviour);

            if (actualType == componentType && actualGuid == componentGuid)
                return componentType;

            _metadataFixes.Add(new MetadataFix
            {
                Path = path,
                ComponentType = actualType,
                ComponentGuid = actualGuid
            });

            if (string.IsNullOrEmpty(componentType) == false && actualType != componentType)
                _log.Info("Root type moved: '{Old}' -> '{New}' for {Path}.", componentType, actualType, path);

            return actualType;
        }

        private static MonoBehaviour FindBehaviour(GameObject prefab, Type resolved, string componentType)
        {
            if (resolved != null)
                return prefab.GetComponent(resolved) as MonoBehaviour;

            // Legacy metadata without a GUID: the type moved namespaces but kept its name.
            var shortName = PrefabCatalogMetadata.ToDisplayName(componentType);
            MonoBehaviour matched = null;

            foreach (var behaviour in prefab.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType().Name != shortName)
                    continue;

                if (matched != null)
                    return null;

                matched = behaviour;
            }

            return matched;
        }

        internal static void ApplyMetadataFixes()
        {
            foreach (var fix in _metadataFixes)
            {
                var importer = AssetImporter.GetAtPath(fix.Path);

                if (importer == null)
                    continue;

                var metadata = PrefabCatalogMetadata.ReadOrDefault(importer);
                metadata.SetRoot(fix.ComponentType, fix.ComponentGuid);
                PrefabCatalogMetadata.Write(importer, metadata);
            }

            _metadataFixes.Clear();
        }

        private struct MetadataFix
        {
            public string Path;
            public string ComponentType;
            public string ComponentGuid;
        }
    }
}
