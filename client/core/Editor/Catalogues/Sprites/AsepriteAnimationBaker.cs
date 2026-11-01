using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace Internal
{
    // Multiple Animations: each animation is its own set of layers, so the importer's ready sprites
    // (merged from all visible layers) do not fit. Frames are composed again and baked
    // into a texture with sprite sub-assets; it is rebuilt only when the file or settings change.
    public static class AsepriteAnimationBaker
    {
        public static string BakedFolder => FrameworkSettings.Content("Runtime/Catalogues/Sprites/Baked");

        private const string LogTag = "AsepriteAnimationBaker";
        private const string HashAssetName = "BakeHash";
        private const int BakeVersion = 2;
        private const int Padding = 1;

        // Empty border around the canvas inside the sprite: the outline is drawn into it (Game/Sprite Outline).
        private const int Border = 1;

        public static List<BakedAnimation> Bake(
            string assetPath,
            string group,
            string fileIdentifier,
            SpriteCatalogMetadata metadata,
            HashSet<string> writtenPaths)
        {
            var result = new List<BakedAnimation>();
            var document = AsepriteDocument.Read(assetPath);
            var requests = ResolveRequests(assetPath, document, metadata);

            if (requests.Count == 0)
            {
                throw new InvalidOperationException($"[{LogTag}] No animations to bake in {assetPath}");
            }

            var frames = new List<FrameKey>();
            var frameIndices = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var request in requests)
            {
                request.FrameIndices = new int[request.Tag.FrameCount];

                for (var i = 0; i < request.Tag.FrameCount; i++)
                {
                    var key = new FrameKey(request.Tag.From + i, request.Layers);

                    if (frameIndices.TryGetValue(key.Id, out var index) == false)
                    {
                        index = frames.Count;
                        frames.Add(key);
                        frameIndices.Add(key.Id, index);
                    }

                    request.FrameIndices[i] = index;
                }
            }

            var settings = BakeSettings.From(AssetImporter.GetAtPath(assetPath));
            var bakedPath = $"{BakedFolder}/{group}_{fileIdentifier}.asset";
            writtenPaths.Add(bakedPath);

            var hash = ComputeHash(assetPath, document, settings, frames);
            var sprites = LoadIfUpToDate(bakedPath, hash, frames.Count) ??
                          Write(bakedPath, hash, fileIdentifier, document, settings, frames);

            foreach (var request in requests)
            {
                var animationSprites = new Sprite[request.FrameIndices.Length];

                for (var i = 0; i < animationSprites.Length; i++)
                    animationSprites[i] = sprites[request.FrameIndices[i]];

                // A custom animation plays at the speed of its source tag.
                var framesPerSecond = metadata.GetFramesPerSecond(request.Tag.Name);
                var time = framesPerSecond > 0f ? 1f / framesPerSecond : 0f;

                result.Add(new BakedAnimation(request.Name, animationSprites, animationSprites.Length * time));
            }

            return result;
        }

        public static void DeleteStale(HashSet<string> writtenPaths)
        {
            if (AssetDatabase.IsValidFolder(BakedFolder) == false)
                return;

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { BakedFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (writtenPaths.Contains(path))
                    continue;

                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[{LogTag}] Deleted stale baked animations: {path}");
            }
        }

        private static List<BakeRequest> ResolveRequests(
            string assetPath,
            AsepriteDocument document,
            SpriteCatalogMetadata metadata)
        {
            var requests = new List<BakeRequest>();

            foreach (var tag in document.Tags)
            {
                var layers = metadata.FindAnimation(tag.Name)?.Layers ?? document.GetVisibleLayerNames();
                requests.Add(new BakeRequest(tag.Name, tag, OrderLayers(document, layers)));
            }

            foreach (var custom in metadata.CustomAnimations)
            {
                var tag = document.FindTag(custom.Source);

                if (tag == null)
                {
                    Debug.LogError(
                        $"[{LogTag}] Custom animation '{custom.Name}' in {assetPath} refers to missing tag '{custom.Source}'");
                    continue;
                }

                requests.Add(new BakeRequest(custom.Name, tag, OrderLayers(document, custom.Layers)));
            }

            return requests;
        }

        // Layer order in the frame key is the file order, so identical sets produce one sprite.
        private static List<string> OrderLayers(AsepriteDocument document, IReadOnlyList<string> selected)
        {
            var ordered = new List<string>();

            foreach (var layer in document.Layers)
            {
                if (selected.Contains(layer.Name))
                    ordered.Add(layer.Name);
            }

            return ordered;
        }

        private static string ComputeHash(
            string assetPath,
            AsepriteDocument document,
            BakeSettings settings,
            IReadOnlyList<FrameKey> frames)
        {
            var hash = new Hash128();
            hash.Append(BakeVersion);
            hash.Append(File.ReadAllBytes(assetPath));
            hash.Append(document.Width);
            hash.Append(document.Height);
            hash.Append(settings.PixelsPerUnit);
            hash.Append(settings.Pivot.x);
            hash.Append(settings.Pivot.y);
            hash.Append((int)settings.FilterMode);

            foreach (var frame in frames)
                hash.Append(frame.Id);

            return hash.ToString();
        }

        private static Sprite[] LoadIfUpToDate(string path, string hash, int frameCount)
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

        private static Sprite[] Write(
            string path,
            string hash,
            string fileIdentifier,
            AsepriteDocument document,
            BakeSettings settings,
            IReadOnlyList<FrameKey> frames)
        {
            var columns = Mathf.CeilToInt(Mathf.Sqrt(frames.Count));
            var rows = Mathf.CeilToInt(frames.Count / (float)columns);
            var spriteWidth = document.Width + Border * 2;
            var spriteHeight = document.Height + Border * 2;
            var strideX = spriteWidth + Padding;
            var strideY = spriteHeight + Padding;
            var width = columns * strideX + Padding;
            var height = rows * strideY + Padding;

            CatalogPaths.EnsureFolder(BakedFolder);

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

        private sealed class BakeRequest
        {
            public BakeRequest(string name, AsepriteTag tag, List<string> layers)
            {
                Name = name;
                Tag = tag;
                Layers = layers;
            }

            public string Name { get; }
            public AsepriteTag Tag { get; }
            public List<string> Layers { get; }
            public int[] FrameIndices { get; set; }
        }

        private sealed class FrameKey
        {
            public FrameKey(int frame, List<string> layers)
            {
                Frame = frame;
                Layers = layers;

                var builder = new StringBuilder();
                builder.Append(frame);

                foreach (var layer in layers)
                    builder.Append('|').Append(layer);

                Id = builder.ToString();
            }

            public int Frame { get; }
            public List<string> Layers { get; }
            public string Id { get; }
        }

        private readonly struct BakeSettings
        {
            private BakeSettings(float pixelsPerUnit, Vector2 pivot, FilterMode filterMode)
            {
                PixelsPerUnit = pixelsPerUnit;
                Pivot = pivot;
                FilterMode = filterMode;
            }

            public float PixelsPerUnit { get; }
            public Vector2 Pivot { get; }
            public FilterMode FilterMode { get; }

            // Frames are baked over the whole canvas, so the importer pivot is relative to the canvas.
            public static BakeSettings From(AssetImporter importer)
            {
                if (importer is not AsepriteImporter aseprite)
                    return new BakeSettings(100f, new Vector2(0.5f, 0f), FilterMode.Point);

                return new BakeSettings(
                    aseprite.spritePixelsPerUnit,
                    ToPivot(aseprite.pivotAlignment, aseprite.customPivotPosition),
                    aseprite.filterMode);
            }

            private static Vector2 ToPivot(SpriteAlignment alignment, Vector2 custom)
            {
                return alignment switch
                {
                    SpriteAlignment.Center => new Vector2(0.5f, 0.5f),
                    SpriteAlignment.TopLeft => new Vector2(0f, 1f),
                    SpriteAlignment.TopCenter => new Vector2(0.5f, 1f),
                    SpriteAlignment.TopRight => new Vector2(1f, 1f),
                    SpriteAlignment.LeftCenter => new Vector2(0f, 0.5f),
                    SpriteAlignment.RightCenter => new Vector2(1f, 0.5f),
                    SpriteAlignment.BottomLeft => new Vector2(0f, 0f),
                    SpriteAlignment.BottomCenter => new Vector2(0.5f, 0f),
                    SpriteAlignment.BottomRight => new Vector2(1f, 0f),
                    _ => custom
                };
            }
        }
    }

    public sealed class BakedAnimation
    {
        public BakedAnimation(string name, Sprite[] sprites, float time)
        {
            Name = name;
            Sprites = sprites;
            Time = time;
        }

        public string Name { get; }
        public Sprite[] Sprites { get; }
        public float Time { get; }
    }
}
