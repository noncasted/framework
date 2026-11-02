using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // The texture asset of a baked file: one sprite per frame, reused while its hash is the same.
    internal static class AsepriteBakedTexture
    {
        private const string LogTag = "AsepriteAnimationBaker";
        private const string HashAssetName = "BakeHash";
        private const int Padding = 1;

        // Empty border around the canvas inside the sprite: the outline is drawn into it (Game/Sprite Outline).
        private const int Border = 1;

        internal static Sprite[] LoadIfUpToDate(string path, string hash, int frameCount)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);

            if (assets == null || assets.Length == 0)
                return null;

            var storedHash = string.Empty;
            var byName = new Dictionary<string, Sprite>(StringComparer.Ordinal);

            foreach (var asset in assets)
            {
                if (asset is TextAsset text && text.name == HashAssetName)
                    storedHash = text.text;
                else if (asset is Sprite sprite)
                    byName[sprite.name] = sprite;
            }

            if (storedHash != hash)
                return null;

            var sprites = new Sprite[frameCount];

            for (var i = 0; i < frameCount; i++)
            {
                if (byName.TryGetValue(SpriteName(path, i), out sprites[i]) == false)
                    return null;
            }

            return sprites;
        }

        internal static Sprite[] Write(
            string folder,
            string path,
            string hash,
            string fileIdentifier,
            AsepriteDocument document,
            AsepriteBakeSettings settings,
            IReadOnlyList<AsepriteFrameKey> frames)
        {
            var columns = Mathf.CeilToInt(Mathf.Sqrt(frames.Count));
            var rows = Mathf.CeilToInt(frames.Count / (float)columns);
            var spriteWidth = document.Width + Border * 2;
            var spriteHeight = document.Height + Border * 2;
            var strideX = spriteWidth + Padding;
            var strideY = spriteHeight + Padding;
            var width = columns * strideX + Padding;
            var height = rows * strideY + Padding;

            CatalogPaths.EnsureFolder(folder);

            // The file is reused so the texture GUID does not change from rebuild to rebuild.
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var isNew = texture == null;

            if (isNew)
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            }
            else
            {
                foreach (var subAsset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                {
                    AssetDatabase.RemoveObjectFromAsset(subAsset);
                    UnityEngine.Object.DestroyImmediate(subAsset, true);
                }

                texture.Reinitialize(width, height, TextureFormat.RGBA32, false);
            }

            texture.name = fileIdentifier;
            texture.filterMode = settings.FilterMode;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(new Color32[width * height]);

            var rects = new Rect[frames.Count];

            for (var i = 0; i < frames.Count; i++)
            {
                // The first frame is in the top-left corner, as in an aseprite sheet.
                var x = Padding + i % columns * strideX;
                var y = height - (i / columns + 1) * strideY;
                texture.SetPixels32(x + Border, y + Border, document.Width, document.Height,
                    document.Compose(frames[i].Frame, frames[i].Layers));
                rects[i] = new Rect(x, y, spriteWidth, spriteHeight);
            }

            texture.Apply(false, false);

            if (isNew)
                AssetDatabase.CreateAsset(texture, path);
            else
                EditorUtility.SetDirty(texture);

            // The pivot is set relative to the canvas; the border shifts it.
            var pivot = new Vector2(
                (settings.Pivot.x * document.Width + Border) / spriteWidth,
                (settings.Pivot.y * document.Height + Border) / spriteHeight);

            var sprites = new Sprite[frames.Count];

            for (var i = 0; i < frames.Count; i++)
            {
                var sprite = Sprite.Create(texture, rects[i], pivot, settings.PixelsPerUnit, 0,
                    SpriteMeshType.FullRect, Vector4.zero, false);
                sprite.name = SpriteName(path, i);
                AssetDatabase.AddObjectToAsset(sprite, texture);
                sprites[i] = sprite;
            }

            AssetDatabase.AddObjectToAsset(new TextAsset(hash) { name = HashAssetName }, texture);
            AssetDatabase.SaveAssetIfDirty(texture);
            Debug.Log($"[{LogTag}] Baked {frames.Count} frame(s) into {path}");
            return sprites;
        }

        private static string SpriteName(string path, int index)
        {
            return $"{Path.GetFileNameWithoutExtension(path)}_{index}";
        }
    }
}
