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
                ReadTagSpeeds(catalog["tagSpeeds"]),
                ReadClips(catalog["clips"]));
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

            if (metadata.Clips.Count > 0)
                catalog["clips"] = WriteClips(metadata.Clips);

            return catalog;
        }

        private static SpriteCatalogKind ParseKind(string kind)
        {
            return kind switch
            {
                null or nameof(SpriteCatalogKind.Sheet) => SpriteCatalogKind.Sheet,
                nameof(SpriteCatalogKind.Animation) => SpriteCatalogKind.Animation,
                nameof(SpriteCatalogKind.MultipleAnimations) => SpriteCatalogKind.MultipleAnimations,

                // Taken for a sheet, it would be written back as a sheet for good.
                _ => throw new InvalidOperationException($"An unknown kind of the sprite markup: {kind}")
            };
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
                    Required(animation, "tag"),
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
                    Required(animation, "name"),
                    Required(animation, "source"),
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

                speeds.Add(new SpriteCatalogTagSpeed(Required(speed, "tag"), RequiredFloat(speed, "fps")));
            }

            return speeds;
        }

        // A clip without its frames or time would play the whole strip or nothing: a broken markup, not a default.
        private static List<SpriteCatalogClip> ReadClips(JToken token)
        {
            var clips = new List<SpriteCatalogClip>();

            if (token is not JArray array)
                return clips;

            foreach (var item in array)
            {
                if (item is not JObject clip)
                    continue;

                clips.Add(new SpriteCatalogClip(
                    Required(clip, "name"),
                    Required(clip, "frames"),
                    RequiredFloat(clip, "time")));
            }

            return clips;
        }

        private static JArray WriteClips(IReadOnlyList<SpriteCatalogClip> clips)
        {
            var array = new JArray();

            foreach (var clip in clips)
            {
                array.Add(new JObject
                {
                    ["name"] = clip.Name ?? string.Empty,
                    ["frames"] = clip.Frames ?? string.Empty,
                    ["time"] = clip.Time
                });
            }

            return array;
        }

        // An entry without its name applies to nothing, is never found and is written back for good: a broken markup, not
        // a default.
        private static string Required(JObject entry, string key)
        {
            return entry.Value<string>(key)
                   ?? throw new InvalidOperationException($"An entry of the sprite markup has no {key}: {entry}");
        }

        // A speed without its value is read as 0 and baked as the duration of every frame: a broken markup, not a default.
        private static float RequiredFloat(JObject entry, string key)
        {
            return entry.Value<float?>(key)
                   ?? throw new InvalidOperationException($"An entry of the sprite markup has no {key}: {entry}");
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
