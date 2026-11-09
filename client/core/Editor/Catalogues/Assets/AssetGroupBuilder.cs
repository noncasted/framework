using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    internal static class AssetGroupBuilder
    {
        private const string LogTag = "AssetCatalogGenerator";

        internal static List<AssetGroupDefinition> BuildGroups(List<AssetSource> sources)
        {
            var groups = new Dictionary<string, AssetGroupDefinition>(StringComparer.Ordinal);
            var usedNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            sources.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));

            foreach (var source in sources)
            {
                if (groups.TryGetValue(source.Group, out var group) == false)
                {
                    group = new AssetGroupDefinition(
                        source.Group,
                        source.Group + "Assets",
                        AssetGroupsRegistry.Instance.IsPreloaded(source.Group));
                    groups.Add(source.Group, group);
                    usedNames.Add(source.Group, new HashSet<string>(StringComparer.Ordinal));
                }

                // Dropping one of the two would silently take an asset out of the catalogue.
                if (usedNames[source.Group].Add(source.PropertyName) == false)
                    throw new InvalidOperationException(
                        $"[{LogTag}] Duplicate identifier '{source.PropertyName}' in group '{source.Group}': " +
                        $"{source.Path}. Rename one of the assets or move it to another group.");

                var type = source.Asset.GetType();
                var script = ScriptTypeNames.ScriptOf(source.Asset);

                group.AddProperty(new AssetPropertyDefinition(
                    source.PropertyName,
                    CatalogNaming.ToFieldName(source.PropertyName),
                    source.Asset,
                    ScriptTypeNames.FullName(type, script).Replace('+', '.'),
                    ScriptTypeNames.AssemblyName(type, script)));
            }

            var result = new List<AssetGroupDefinition>();

            foreach (var group in groups.Values)
            {
                if (group.Properties.Count == 0)
                    continue;

                group.SortProperties();
                result.Add(group);
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            AssignGeneratedTargets(result);
            return result;
        }

        // A group class must live in an assembly that sees the types of its assets,
        // so a group that mixes assemblies is not code-generated at all.
        internal static void AssignGeneratedTargets(List<AssetGroupDefinition> groups)
        {
            foreach (var group in groups)
            {
                var assemblies = new SortedSet<string>(StringComparer.Ordinal);

                foreach (var property in group.Properties)
                {
                    if (CatalogAssemblies.IsConsumerAssembly(property.TypeAssembly))
                        assemblies.Add(property.TypeAssembly);
                }

                if (assemblies.Count > 1)
                {
                    Debug.LogError(
                        $"[{LogTag}] Group '{group.Name}' mixes asset types from {string.Join(", ", assemblies)}. One group must stay in one assembly.");
                    group.DisableCodegen();
                    continue;
                }

                if (assemblies.Count == 1)
                {
                    group.SetGeneratedTarget(
                        CatalogAssemblies.ToNamespace(LogTag, assemblies.Min),
                        AssetsCatalogClassGenerator.GetAssemblyGeneratedFolder(assemblies.Min));
                    continue;
                }

                group.SetGeneratedTarget("Internal", AssetsCatalogClassGenerator.InternalGeneratedFolder);
            }
        }
    }
}
