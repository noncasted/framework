using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // Groups the clips with unique property names and collects Name_0, Name_1 into variation lists.
    internal static class AudioGroupBuilder
    {
        private const string LogTag = "AudioCatalogGenerator";

        internal static List<AudioGroupDefinition> BuildGroups(List<ClipSource> sources)
        {
            sources.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));

            var groups = new Dictionary<string, AudioGroupDefinition>(StringComparer.Ordinal);
            var usedNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var source in sources)
            {
                if (groups.TryGetValue(source.Group, out var group) == false)
                {
                    group = new AudioGroupDefinition(source.Group, source.Group + "Audio");
                    groups.Add(source.Group, group);
                    usedNames.Add(source.Group, new HashSet<string>(StringComparer.Ordinal));
                }

                // Dropping one of the two would silently take a sound out of the catalogue.
                if (usedNames[source.Group].Add(source.PropertyName) == false)
                    throw new InvalidOperationException(
                        $"[{LogTag}] Duplicate identifier '{source.PropertyName}' in group '{source.Group}': " +
                        $"{source.Path}. Rename one of the clips or move it to another group.");

                group.AddProperty(new AudioPropertyDefinition(
                    source.PropertyName,
                    CatalogNaming.ToFieldName(source.PropertyName),
                    source.Variation,
                    source.Clip,
                    source.Encoded,
                    source.Volume));
            }

            var result = new List<AudioGroupDefinition>(groups.Values);

            foreach (var group in result)
            {
                group.SortProperties();
                BuildVariations(group, usedNames[group.Name]);
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            return result;
        }

        private static void BuildVariations(AudioGroupDefinition group, HashSet<string> usedNames)
        {
            var variations = new Dictionary<string, AudioVariationDefinition>(StringComparer.Ordinal);

            foreach (var property in group.Properties)
            {
                var key = property.Variation;

                if (key == null)
                    continue;

                if (variations.TryGetValue(key.Name, out var variation) == false)
                {
                    if (usedNames.Contains(key.Name))
                    {
                        Debug.LogError(
                            $"[{LogTag}] Variation list '{key.Name}' in group '{group.Name}' clashes with a single clip. Skipping variations.");
                        variations.Add(key.Name, null);
                        continue;
                    }

                    variation = new AudioVariationDefinition(key.Name, CatalogNaming.ToFieldName(key.Name));
                    variations.Add(key.Name, variation);
                    group.AddVariation(variation);
                }

                variation?.AddEntry(property);
            }

            foreach (var variation in group.Variations)
                variation.SortEntries();

            group.SortVariations();
        }
    }
}
