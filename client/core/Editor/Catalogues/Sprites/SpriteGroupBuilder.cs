using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Internal
{
    // Sorts the sources by priority and turns them into groups with unique property names.
    internal static class SpriteGroupBuilder
    {
        private static readonly ILog _log = Logs.Of(typeof(SpriteGroupBuilder));

        internal static List<SpriteGroupDefinition> BuildGroups(List<SpriteSource> sources)
        {
            sources.Sort(CompareSources);

            var groups = new Dictionary<string, SpriteGroupDefinition>(StringComparer.Ordinal);
            var usedNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var source in sources)
            {
                if (groups.TryGetValue(source.Group, out var group) == false)
                {
                    group = new SpriteGroupDefinition(
                        CatalogNaming.ToGroupKey(source.Group),
                        source.Group,
                        CatalogNaming.ToGroupKey(source.Group) + "Sprites");
                    groups.Add(source.Group, group);
                    usedNames.Add(source.Group, new HashSet<string>(StringComparer.Ordinal));
                }

                if (source.IsMultipleAnimations)
                {
                    // The file's animations are a nested object of the group: Sprites.Game.Players.Grand.IdleFace.
                    // In the group asset the key stays flat ("GrandIdleFace"); it is unique within the group.
                    if (usedNames[source.Group].Add(source.PropertyName) == false)
                    {
                        _log.Error("Duplicate identifier '{Name}' in group '{Group}'. Skipping {Path}.", source.PropertyName,
                            group.Path, source.Path);
                        continue;
                    }

                    foreach (var animation in source.BakedAnimations)
                    {
                        var memberName = CatalogNaming.ToGroupName(animation.Name);
                        var propertyName = source.PropertyName + memberName;

                        if (TryAddProperty(
                                group,
                                usedNames[source.Group],
                                propertyName,
                                SpriteKind.Animation,
                                animation.Sprites,
                                animation.Time,
                                source.Color,
                                source.Path))
                        {
                            group.Properties[group.Properties.Count - 1].NestInOwner(
                                source.PropertyName,
                                memberName,
                                CatalogNaming.ToFieldName(memberName));
                        }
                    }

                    continue;
                }

                var sprites = SpriteSheetLoader.CollectSprites(source.Path, source.Kind);

                if (sprites.Length == 0)
                    throw new InvalidOperationException($"No sprites found in the included source {source.Path}");

                if (source.Kind == SpriteKind.Animation)
                {
                    TryAddProperty(
                        group,
                        usedNames[source.Group],
                        source.PropertyName,
                        SpriteKind.Animation,
                        sprites,
                        source.Time,
                        source.Color,
                        source.Path);
                    continue;
                }

                for (var i = 0; i < sprites.Length; i++)
                {
                    var sprite = sprites[i];
                    var propertyName = ToSheetPropertyName(source.Path, sprite.name);

                    if (string.IsNullOrEmpty(propertyName))
                    {
                        _log.Error("Identifier is empty for sprite '{Sprite}' in {Path}", sprite.name, source.Path);
                        continue;
                    }

                    TryAddProperty(
                        group,
                        usedNames[source.Group],
                        propertyName,
                        SpriteKind.Sheet,
                        new[] { sprite },
                        source.Time,
                        source.Color,
                        source.Path);
                }
            }

            var result = new List<SpriteGroupDefinition>();

            foreach (var group in groups.Values)
            {
                if (group.Properties.Count == 0)
                    continue;

                group.SortProperties();
                result.Add(group);
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            return result;
        }

        private static int CompareSources(SpriteSource left, SpriteSource right)
        {
            var group = string.Compare(left.Group, right.Group, StringComparison.Ordinal);

            if (group != 0)
                return group;

            var priority = left.Priority.CompareTo(right.Priority);

            if (priority != 0)
                return priority;

            return string.Compare(left.Path, right.Path, StringComparison.Ordinal);
        }

        private static bool TryAddProperty(
            SpriteGroupDefinition group,
            HashSet<string> usedNames,
            string propertyName,
            SpriteKind kind,
            Sprite[] sprites,
            float time,
            Color color,
            string sourcePath)
        {
            if (usedNames.Add(propertyName) == false)
            {
                _log.Error("Duplicate identifier '{Name}' in group '{Group}'. Skipping lower-priority source {Path}.",
                    propertyName, group.Name, sourcePath);
                return false;
            }

            group.AddProperty(new SpritePropertyDefinition(
                propertyName,
                CatalogNaming.ToFieldName(propertyName),
                kind,
                sprites,
                time,
                color));
            return true;
        }

        private static string ToSheetPropertyName(string assetPath, string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
                return string.Empty;

            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            var remainder = spriteName;

            if (string.IsNullOrEmpty(fileName) == false &&
                remainder.StartsWith(fileName, StringComparison.OrdinalIgnoreCase))
            {
                remainder = remainder.Substring(fileName.Length);

                if (remainder.StartsWith("_") || remainder.StartsWith("-") || remainder.StartsWith(" "))
                    remainder = remainder.Substring(1);
            }

            if (string.IsNullOrWhiteSpace(remainder))
                remainder = spriteName;

            return CatalogNaming.ToGroupName(remainder);
        }
    }
}
