using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
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
        private const int BakeVersion = 2;

        public static List<BakedAnimation> Bake(
            string assetPath,
            string group,
            string fileIdentifier,
            SpriteCatalogMetadata metadata,
            HashSet<string> writtenPaths)
        {
            var result = new List<BakedAnimation>();
            var document = AsepriteDocument.Read(assetPath);
            var requests = AsepriteBakePlan.ResolveRequests(assetPath, document, metadata);

            if (requests.Count == 0)
            {
                throw new InvalidOperationException($"[{LogTag}] No animations to bake in {assetPath}");
            }

            var frames = AsepriteBakePlan.CollectFrames(requests);
            var settings = AsepriteBakeSettings.From(AssetImporter.GetAtPath(assetPath));
            var bakedPath = $"{BakedFolder}/{group}_{fileIdentifier}.asset";
            writtenPaths.Add(bakedPath);

            var hash = ComputeHash(assetPath, document, settings, frames);
            var sprites = AsepriteBakedTexture.LoadIfUpToDate(bakedPath, hash, frames.Count) ??
                          AsepriteBakedTexture.Write(BakedFolder, bakedPath, hash, fileIdentifier, document, settings, frames);

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

        private static string ComputeHash(
            string assetPath,
            AsepriteDocument document,
            AsepriteBakeSettings settings,
            IReadOnlyList<AsepriteFrameKey> frames)
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
    }
}
