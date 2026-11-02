using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    internal static class SpriteSheetLoader
    {
        internal static Sprite[] CollectSprites(string assetPath, SpriteKind kind)
        {
            var sprites = LoadSprites(assetPath);

            if (sprites.Count == 0)
                return Array.Empty<Sprite>();

            if (kind == SpriteKind.Sheet)
                return ToArray(sprites);

            return OrderAnimationSprites(assetPath, sprites);
        }

        private static List<Sprite> LoadSprites(string assetPath)
        {
            var sprites = new List<Sprite>();
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

            if (assets == null)
                return sprites;

            foreach (var asset in assets)
            {
                if (asset is Sprite sprite)
                    sprites.Add(sprite);
            }

            return sprites;
        }

        private static Sprite[] OrderAnimationSprites(string assetPath, IReadOnlyList<Sprite> sprites)
        {
            var names = ReadFrameNames(assetPath);

            if (names.Count == 0)
                return ToArray(sprites);

            var byName = new Dictionary<string, Sprite>(StringComparer.Ordinal);

            for (var i = 0; i < sprites.Count; i++)
            {
                if (byName.ContainsKey(sprites[i].name) == false)
                    byName.Add(sprites[i].name, sprites[i]);
            }

            var ordered = new List<Sprite>(names.Count);

            for (var i = 0; i < names.Count; i++)
            {
                if (byName.TryGetValue(names[i], out var sprite))
                    ordered.Add(sprite);
            }

            return ordered.Count > 0 ? ordered.ToArray() : ToArray(sprites);
        }

        private static IReadOnlyList<string> ReadFrameNames(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath);

            if (importer == null)
                return Array.Empty<string>();

            if (SpriteCatalogMetadata.IsAsepriteImporter(importer))
            {
                var names = ReadSerializedNames(importer, "m_AnimatedSpriteImportData");

                if (names.Count > 0)
                    return names;
            }

            return ReadSerializedNames(importer, "m_SpriteSheet.m_Sprites");
        }

        private static IReadOnlyList<string> ReadSerializedNames(AssetImporter importer, string propertyName)
        {
            var serializedObject = new SerializedObject(importer);
            var frames = serializedObject.FindProperty(propertyName);

            if (frames == null || frames.isArray == false || frames.arraySize == 0)
                return Array.Empty<string>();

            var names = new List<string>(frames.arraySize);

            for (var i = 0; i < frames.arraySize; i++)
            {
                var nameProperty = frames.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name");

                if (nameProperty == null)
                    nameProperty = frames.GetArrayElementAtIndex(i).FindPropertyRelative("name");

                if (nameProperty == null || string.IsNullOrEmpty(nameProperty.stringValue))
                    continue;

                names.Add(nameProperty.stringValue);
            }

            return names;
        }

        private static Sprite[] ToArray(IReadOnlyList<Sprite> sprites)
        {
            var array = new Sprite[sprites.Count];

            for (var i = 0; i < sprites.Count; i++)
                array[i] = sprites[i];

            return array;
        }
    }
}
