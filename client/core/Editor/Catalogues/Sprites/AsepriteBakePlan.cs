using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Internal
{
    // What a file bakes into: the animations of its tags and custom animations, and the unique frames they use.
    internal static class AsepriteBakePlan
    {
        private const string LogTag = "AsepriteAnimationBaker";

        internal static List<AsepriteBakeRequest> ResolveRequests(
            string assetPath,
            AsepriteDocument document,
            SpriteCatalogMetadata metadata)
        {
            var requests = new List<AsepriteBakeRequest>();

            foreach (var tag in document.Tags)
            {
                var layers = metadata.FindAnimation(tag.Name)?.Layers ?? document.GetVisibleLayerNames();
                requests.Add(new AsepriteBakeRequest(tag.Name, tag, OrderLayers(document, layers)));
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

                requests.Add(new AsepriteBakeRequest(custom.Name, tag, OrderLayers(document, custom.Layers)));
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

        // An equal frame with equal layers is one sprite of the texture.
        internal static List<AsepriteFrameKey> CollectFrames(IReadOnlyList<AsepriteBakeRequest> requests)
        {
            var frames = new List<AsepriteFrameKey>();
            var frameIndices = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var request in requests)
            {
                var indices = new int[request.Tag.FrameCount];
                request.SetFrameIndices(indices);

                for (var i = 0; i < request.Tag.FrameCount; i++)
                {
                    var key = new AsepriteFrameKey(request.Tag.From + i, request.Layers);

                    if (frameIndices.TryGetValue(key.Id, out var index) == false)
                    {
                        index = frames.Count;
                        frames.Add(key);
                        frameIndices.Add(key.Id, index);
                    }

                    indices[i] = index;
                }
            }

            return frames;
        }
    }
}
