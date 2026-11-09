using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    internal static class SpriteGroupAssetWriter
    {
        internal static void WriteGroupAssets(IReadOnlyList<SpriteGroupDefinition> groups, string groupsFolder)
        {
            CatalogPaths.EnsureFolder(groupsFolder);

            var writtenPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var path = $"{groupsFolder}/{group.Name}.asset";
                writtenPaths.Add(path);
                WriteGroupAsset(path, group);
            }

            CatalogGroupAssets.DeleteStale<SpriteGroupAsset>(groupsFolder, writtenPaths, "SpriteGenerator");
        }

        private static void WriteGroupAsset(string path, SpriteGroupDefinition group)
        {
            var asset = AssetDatabase.LoadAssetAtPath<SpriteGroupAsset>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SpriteGroupAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            if (EntriesMatch(asset, group.Properties))
            {
                group.SetGuid(AssetDatabase.AssetPathToGUID(path));
                return;
            }

            ApplyEntries(asset, group.Properties);
            EditorUtility.SetDirty(asset);
            group.SetGuid(AssetDatabase.AssetPathToGUID(path));
        }

        private static bool EntriesMatch(SpriteGroupAsset asset, IReadOnlyList<SpritePropertyDefinition> properties)
        {
            var serializedObject = new SerializedObject(asset);
            var entries = serializedObject.FindProperty("_entries");

            if (entries == null || entries.isArray == false)
                return false;

            if (entries.arraySize != properties.Count)
                return false;

            for (var i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                var entry = entries.GetArrayElementAtIndex(i);

                if (entry.FindPropertyRelative(SpriteEntry.NameKey).stringValue != property.PropertyName)
                    return false;

                if (entry.FindPropertyRelative(SpriteEntry.KindKey).enumValueIndex != (int)property.Kind)
                    return false;

                if (Mathf.Approximately(entry.FindPropertyRelative(SpriteEntry.TimeKey).floatValue, property.Time) == false)
                    return false;

                if (entry.FindPropertyRelative(SpriteEntry.ColorKey).colorValue != property.Color)
                    return false;

                var sprites = entry.FindPropertyRelative(SpriteEntry.SpritesKey);

                if (sprites == null || sprites.isArray == false || sprites.arraySize != property.Sprites.Length)
                    return false;

                for (var spriteIndex = 0; spriteIndex < property.Sprites.Length; spriteIndex++)
                {
                    if (sprites.GetArrayElementAtIndex(spriteIndex).objectReferenceValue !=
                        property.Sprites[spriteIndex])
                        return false;
                }
            }

            return true;
        }

        private static void ApplyEntries(SpriteGroupAsset asset, IReadOnlyList<SpritePropertyDefinition> properties)
        {
            var serializedObject = new SerializedObject(asset);
            var entries = serializedObject.FindProperty("_entries");
            entries.arraySize = properties.Count;

            for (var i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative(SpriteEntry.NameKey).stringValue = property.PropertyName;
                entry.FindPropertyRelative(SpriteEntry.KindKey).enumValueIndex = (int)property.Kind;
                entry.FindPropertyRelative(SpriteEntry.TimeKey).floatValue = property.Time;
                entry.FindPropertyRelative(SpriteEntry.ColorKey).colorValue = property.Color;

                var sprites = entry.FindPropertyRelative(SpriteEntry.SpritesKey);
                sprites.arraySize = property.Sprites.Length;

                for (var spriteIndex = 0; spriteIndex < property.Sprites.Length; spriteIndex++)
                    sprites.GetArrayElementAtIndex(spriteIndex).objectReferenceValue = property.Sprites[spriteIndex];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
