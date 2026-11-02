using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Internal
{
    // The format of the sprite markup in the userData of an importer: the games keep these names in their .meta files.
    internal static class SpriteCatalogMetadataJson
    {
        internal static SpriteCatalogMetadata Read(JObject catalog)
        {
            return new SpriteCatalogMetadata(
                catalog["included"] != null && catalog.Value<bool>("included"),
                catalog.Value<string>("group") ?? string.Empty,
                ParseKind(catalog.Value<string>("kind")),
                catalog["time"] != null ? catalog.Value<float>("time") : SpriteCatalogMetadata.DefaultTime,
                ReadColor(catalog["color"]),
                catalog["fps"] != null ? catalog.Value<float>("fps") : SpriteCatalogMetadata.DefaultFramesPerSecond,
                ReadAnimations(catalog["animations"]),
                ReadCustomAnimations(catalog["customAnimations"]),
                ReadTagSpeeds(catalog["tagSpeeds"]));
        }

        internal static JObject Write(SpriteCatalogMetadata metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            var catalog = new JObject
            {
                ["included"] = metadata.Included,
                ["group"] = metadata.Group ?? string.Empty,
                ["kind"] = metadata.Kind.ToString(),
                ["time"] = metadata.Time,
                ["color"] = new JObject
                {
                    ["r"] = metadata.Color.r,
                    ["g"] = metadata.Color.g,
                    ["b"] = metadata.Color.b,
                    ["a"] = metadata.Color.a
                }
            };

            if (metadata.Kind == SpriteCatalogKind.MultipleAnimations ||
                metadata.Animations.Count > 0 ||
                metadata.CustomAnimations.Count > 0)
            {
                catalog["fps"] = metadata.FramesPerSecond;
                catalog["animations"] = WriteAnimations(metadata.Animations);
                catalog["customAnimations"] = WriteCustomAnimations(metadata.CustomAnimations);

                if (metadata.TagSpeeds.Count > 0)
                    catalog["tagSpeeds"] = WriteTagSpeeds(metadata.TagSpeeds);
            }

            return catalog;
        }

        private static SpriteCatalogKind ParseKind(string kind)
        {
            if (string.Equals(kind, nameof(SpriteCatalogKind.Animation), StringComparison.Ordinal))
                return SpriteCatalogKind.Animation;

            if (string.Equals(kind, nameof(SpriteCatalogKind.MultipleAnimations), StringComparison.Ordinal))
                return SpriteCatalogKind.MultipleAnimations;

            return SpriteCatalogKind.Sheet;
        }

        private static List<SpriteCatalogAnimation> ReadAnimations(JToken token)
        {
            var animations = new List<SpriteCatalogAnimation>();

            if (token is not JArray array)
                return animations;

            foreach (var item in array)
            {
                if (item is not JObject animation)
                    continue;

                animations.Add(new SpriteCatalogAnimation(
                    animation.Value<string>("tag") ?? string.Empty,
                    ReadStrings(animation["layers"])));
            }

            return animations;
        }

        private static List<SpriteCatalogCustomAnimation> ReadCustomAnimations(JToken token)
        {
            var animations = new List<SpriteCatalogCustomAnimation>();

            if (token is not JArray array)
                return animations;

            foreach (var item in array)
            {
                if (item is not JObject animation)
                    continue;

                animations.Add(new SpriteCatalogCustomAnimation(
                    animation.Value<string>("name") ?? string.Empty,
                    animation.Value<string>("source") ?? string.Empty,
                    ReadStrings(animation["layers"])));
            }

            return animations;
        }

        private static List<SpriteCatalogTagSpeed> ReadTagSpeeds(JToken token)
        {
            var speeds = new List<SpriteCatalogTagSpeed>();

            if (token is not JArray array)
                return speeds;

            foreach (var item in array)
            {
                if (item is not JObject speed)
                    continue;

                // A speed of no tag is applied to nothing and is written back for good: a broken markup, not a default.
                var tag = speed.Value<string>("tag") ?? throw new InvalidOperationException($"A tag speed of the sprite markup has no tag: {speed}");

                speeds.Add(new SpriteCatalogTagSpeed(tag, speed.Value<float>("fps")));
            }

            return speeds;
        }

        private static JArray WriteTagSpeeds(IReadOnlyList<SpriteCatalogTagSpeed> speeds)
        {
            var array = new JArray();

            foreach (var speed in speeds)
            {
                array.Add(new JObject
                {
                    ["tag"] = speed.Tag,
                    ["fps"] = speed.FramesPerSecond
                });
            }

            return array;
        }

        private static List<string> ReadStrings(JToken token)
        {
            var strings = new List<string>();

            if (token is not JArray array)
                return strings;

            foreach (var item in array)
            {
                if (item.Type == JTokenType.String)
                    strings.Add(item.Value<string>());
            }

            return strings;
        }

        private static JArray WriteAnimations(IReadOnlyList<SpriteCatalogAnimation> animations)
        {
            var array = new JArray();

            foreach (var animation in animations)
            {
                array.Add(new JObject
                {
                    ["tag"] = animation.Tag ?? string.Empty,
                    ["layers"] = new JArray(animation.Layers)
                });
            }

            return array;
        }

        private static JArray WriteCustomAnimations(IReadOnlyList<SpriteCatalogCustomAnimation> animations)
        {
            var array = new JArray();

            foreach (var animation in animations)
            {
                array.Add(new JObject
                {
                    ["name"] = animation.Name ?? string.Empty,
                    ["source"] = animation.Source ?? string.Empty,
                    ["layers"] = new JArray(animation.Layers)
                });
            }

            return array;
        }

        private static Color ReadColor(JToken token)
        {
            if (token is not JObject color)
                return SpriteCatalogMetadata.DefaultColor;

            return new Color(
                    color["r"] != null ? color.Value<float>("r") : 1f,
                    color["g"] != null ? color.Value<float>("g") : 1f,
                    color["b"] != null ? color.Value<float>("b") : 1f,
                    color["a"] != null ? color.Value<float>("a") : 1f
                );
        }
    }
}
