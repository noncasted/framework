using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // Groups the sources with unique property names and picks the assembly the class of each group is generated in.
    internal static class PrefabGroupBuilder
    {
        internal static List<PrefabGroupDefinition> BuildGroups(List<PrefabSource> sources)
        {
            sources.Sort((left, right) => {
                var group = string.Compare(left.Group, right.Group, StringComparison.Ordinal);

                if (group != 0)
                    return group;

                return string.Compare(left.Path, right.Path, StringComparison.Ordinal);
            });

            var groups = new Dictionary<string, PrefabGroupDefinition>(StringComparer.Ordinal);
            var usedNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var source in sources)
            {
                if (groups.TryGetValue(source.Group, out var group) == false)
                {
                    group = new PrefabGroupDefinition(
                        source.Group,
                        source.Group + "Prefabs",
                        PrefabGroupsRegistry.Instance.IsPreloaded(source.Group));
                    groups.Add(source.Group, group);
                    usedNames.Add(source.Group, new HashSet<string>(StringComparer.Ordinal));
                }

                // Dropping one of the two would silently take a prefab out of the catalogue.
                if (usedNames[source.Group].Add(source.PropertyName) == false)
                    throw new InvalidOperationException(
                        $"[PrefabCatalogGenerator] Duplicate identifier '{source.PropertyName}' in group " +
                        $"'{source.Group}': {source.Path}. Rename one of the prefabs or move it to another group.");

                group.AddProperty(new PrefabPropertyDefinition(
                    source.PropertyName,
                    CatalogNaming.ToFieldName(source.PropertyName),
                    source.Prefab,
                    source.ComponentType,
                    ToComponentDisplayName(source.ComponentType)));
            }

            var result = new List<PrefabGroupDefinition>();

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

        private static void AssignGeneratedTargets(List<PrefabGroupDefinition> groups)
        {
            foreach (var group in groups)
            {
                var assemblies = new SortedSet<string>(StringComparer.Ordinal);

                foreach (var property in group.Properties)
                {
                    FillPropertyType(property);

                    if (CatalogAssemblies.IsConsumerAssembly(property.TypeAssembly))
                        assemblies.Add(property.TypeAssembly);
                }

                if (assemblies.Count > 1)
                {
                    Debug.LogError(
                            $"[PrefabCatalogGenerator] Group '{group.Name}' mixes root types from {string.Join(", ", assemblies)}. One group must stay in one assembly."
                        );
                    group.DisableCodegen();
                    continue;
                }

                group.SetGeneratedFolder(assemblies.Count == 1
                    ? PrefabsCatalogClassGenerator.GetAssemblyGeneratedFolder(assemblies.Min)
                    : PrefabsCatalogClassGenerator.InternalGeneratedFolder);
            }
        }

        private static void FillPropertyType(PrefabPropertyDefinition property)
        {
            if (string.IsNullOrEmpty(property.ComponentType))
            {
                property.SetGameObjectType();
                return;
            }

            CatalogAssemblies.ParseTypeName(property.ComponentType, out var fullName, out var assemblyName);
            property.SetComponentType(fullName.Replace('+', '.'), assemblyName);
        }

        private static string ToComponentDisplayName(string componentType)
        {
            if (string.IsNullOrEmpty(componentType))
                return "UnityEngine.GameObject";

            var display = componentType;
            var comma = display.IndexOf(',');

            if (comma > 0)
                display = display.Substring(0, comma).Trim();

            return display;
        }
    }
}
