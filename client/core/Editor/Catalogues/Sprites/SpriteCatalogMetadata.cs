using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    public enum SpriteCatalogKind
    {
        Sheet,
        Animation,
        MultipleAnimations
    }

    // Animation from an aseprite tag: the name comes from the tag, only the enabled layers are configurable.
    public sealed class SpriteCatalogAnimation
    {
        private readonly List<string> _layers;

        public SpriteCatalogAnimation(string tag, List<string> layers)
        {
            Tag = tag;
            _layers = layers;
        }

        public string Tag { get; }
        public IReadOnlyList<string> Layers => _layers;

        public void SetLayer(string layer, bool enabled)
        {
            SpriteCatalogMetadata.SetLayer(_layers, layer, enabled);
        }
    }

    // Own speed of one aseprite tag: its animation and every custom animation made from it play at this FPS
    // instead of the FPS of the file.
    public sealed class SpriteCatalogTagSpeed
    {
        public SpriteCatalogTagSpeed(string tag, float framesPerSecond)
        {
            Tag = tag;
            FramesPerSecond = framesPerSecond;
        }

        public string Tag { get; }
        public float FramesPerSecond { get; }
    }

    // Additional animation on top of a source tag with its own name and layer set.
    public sealed class SpriteCatalogCustomAnimation
    {
        private readonly List<string> _layers;

        public SpriteCatalogCustomAnimation(string name, string source, List<string> layers)
        {
            Name = name;
            Source = source;
            _layers = layers;
        }

        public string Name { get; private set; }
        public string Source { get; private set; }
        public IReadOnlyList<string> Layers => _layers;

        public void Rename(string name)
        {
            Name = name;
        }

        public void SetSource(string source)
        {
            Source = source;
        }

        public void SetLayer(string layer, bool enabled)
        {
            SpriteCatalogMetadata.SetLayer(_layers, layer, enabled);
        }
    }

    public sealed class SpriteCatalogMetadata
    {
        public const float DefaultTime = 0.8f;
        public const float DefaultFramesPerSecond = 12f;
        public static readonly Color DefaultColor = Color.white;

        private const string UserDataKey = "spriteCatalog";
        private const string LogTag = "SpriteCatalogMetadata";

        private List<SpriteCatalogAnimation> _animations = new();
        private List<SpriteCatalogCustomAnimation> _customAnimations = new();
        private List<SpriteCatalogTagSpeed> _tagSpeeds = new();

        public bool Included { get; private set; }
        public string Group { get; private set; } = string.Empty;
        public SpriteCatalogKind Kind { get; private set; } = SpriteCatalogKind.Sheet;
        public float Time { get; private set; } = DefaultTime;
        public Color Color { get; private set; } = DefaultColor;
        public float FramesPerSecond { get; private set; } = DefaultFramesPerSecond;

        // Only tags whose layer set is already saved; the others use the file's visible layers.
        public IReadOnlyList<SpriteCatalogAnimation> Animations => _animations;
        public IReadOnlyList<SpriteCatalogCustomAnimation> CustomAnimations => _customAnimations;

        // Only tags with their own speed; the others play at FramesPerSecond.
        public IReadOnlyList<SpriteCatalogTagSpeed> TagSpeeds => _tagSpeeds;

        public void SetIncluded(bool included)
        {
            Included = included;
        }

        public void SetGroup(string group)
        {
            Group = group;
        }

        public void SetKind(SpriteCatalogKind kind)
        {
            Kind = kind;
        }

        public void SetTime(float time)
        {
            Time = time;
        }

        public void SetColor(Color color)
        {
            Color = color;
        }

        public void SetFramesPerSecond(float framesPerSecond)
        {
            FramesPerSecond = framesPerSecond;
        }

        // Time and color of another kind do not carry over to an animation.
        public void SwitchToAnimation()
        {
            if (Kind != SpriteCatalogKind.Animation)
            {
                Time = DefaultTime;
                Color = DefaultColor;
            }

            Kind = SpriteCatalogKind.Animation;
        }

        public void AddAnimation(SpriteCatalogAnimation animation)
        {
            _animations.Add(animation);
        }

        public void AddCustomAnimation(SpriteCatalogCustomAnimation animation)
        {
            _customAnimations.Add(animation);
        }

        public void RemoveCustomAnimationAt(int index)
        {
            _customAnimations.RemoveAt(index);
        }

        public static bool TryRead(AssetImporter importer, out SpriteCatalogMetadata metadata)
        {
            metadata = null;

            if (AssetUserData.TryRead(importer, UserDataKey, LogTag, out var catalog) == false)
                return false;

            metadata = new SpriteCatalogMetadata
            {
                Included = catalog["included"] != null && catalog.Value<bool>("included"),
                Group = catalog.Value<string>("group") ?? string.Empty,
                Kind = ParseKind(catalog.Value<string>("kind")),
                Time = catalog["time"] != null ? catalog.Value<float>("time") : DefaultTime,
                Color = ReadColor(catalog["color"]),
                FramesPerSecond = catalog["fps"] != null ? catalog.Value<float>("fps") : DefaultFramesPerSecond,
                _animations = ReadAnimations(catalog["animations"]),
                _customAnimations = ReadCustomAnimations(catalog["customAnimations"]),
                _tagSpeeds = ReadTagSpeeds(catalog["tagSpeeds"])
            };
            return true;
        }

        public static SpriteCatalogMetadata ReadOrDefault(AssetImporter importer)
        {
            if (TryRead(importer, out var metadata))
                return metadata;

            return CreateDefault(importer);
        }

        public static SpriteCatalogMetadata CreateDefault(AssetImporter importer)
        {
            if (importer == null)
                throw new ArgumentNullException(nameof(importer));

            return new SpriteCatalogMetadata
            {
                Group = GetDefaultGroup(importer.assetPath),
                Kind = GetDefaultKind(importer),
                Time = DefaultTime,
                Color = DefaultColor
            };
        }

        private static JObject ToJson(SpriteCatalogMetadata metadata)
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

        public static void Write(AssetImporter importer, SpriteCatalogMetadata metadata)
        {
            if (AssetUserData.Write(importer, UserDataKey, LogTag, ToJson(metadata)))
                SpriteGroupsRegistry.Instance.Invalidate();
        }

        // Change from the inspector: stays in the importer's memory until Apply.
        public static void Stage(AssetImporter importer, SpriteCatalogMetadata metadata)
        {
            if (AssetUserData.Stage(importer, UserDataKey, LogTag, ToJson(metadata)))
                SpriteGroupsRegistry.Instance.Invalidate();
        }

        public SpriteCatalogAnimation FindAnimation(string tag)
        {
            foreach (var animation in Animations)
            {
                if (string.Equals(animation.Tag, tag, StringComparison.Ordinal))
                    return animation;
            }

            return null;
        }

        public float GetFramesPerSecond(string tag)
        {
            foreach (var speed in TagSpeeds)
            {
                if (string.Equals(speed.Tag, tag, StringComparison.Ordinal))
                    return speed.FramesPerSecond;
            }

            return FramesPerSecond;
        }

        // The speed of the file itself removes the entry: the tag follows the file again.
        public void SetFramesPerSecond(string tag, float framesPerSecond)
        {
            _tagSpeeds.RemoveAll(speed => string.Equals(speed.Tag, tag, StringComparison.Ordinal));

            if (Mathf.Approximately(framesPerSecond, FramesPerSecond) == true)
                return;

            _tagSpeeds.Add(new SpriteCatalogTagSpeed(tag, framesPerSecond));
        }

        public static string GetDefaultGroup(string assetPath)
        {
            var artPrefix = FrameworkSettings.ArtFolder + "/";

            if (string.IsNullOrEmpty(assetPath) ||
                assetPath.StartsWith(artPrefix, StringComparison.OrdinalIgnoreCase) == false)
                return string.Empty;

            var relative = assetPath.Substring(artPrefix.Length);
            var directory = Path.GetDirectoryName(relative);

            if (string.IsNullOrEmpty(directory))
                return string.Empty;

            return CatalogNaming.ToGroupPath(directory);
        }

        public static SpriteCatalogKind GetDefaultKind(AssetImporter importer)
        {
            if (importer == null)
                throw new ArgumentNullException(nameof(importer));

            if (IsAsepriteImporter(importer) && HasMultipleAsepriteFrames(importer))
                return SpriteCatalogKind.Animation;

            return SpriteCatalogKind.Sheet;
        }

        public static bool IsAsepriteImporter(AssetImporter importer)
        {
            if (importer == null)
                return false;

            if (importer.GetType().Name == "AsepriteImporter")
                return true;

            var extension = Path.GetExtension(importer.assetPath);

            return extension.Equals(".aseprite", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".ase", StringComparison.OrdinalIgnoreCase);
        }

        internal static void SetLayer(List<string> layers, string layer, bool enabled)
        {
            layers.Remove(layer);

            if (enabled)
                layers.Add(layer);
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

                speeds.Add(new SpriteCatalogTagSpeed(speed.Value<string>("tag"), speed.Value<float>("fps")));
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
                return DefaultColor;

            return new Color(
                    color["r"] != null ? color.Value<float>("r") : 1f,
                    color["g"] != null ? color.Value<float>("g") : 1f,
                    color["b"] != null ? color.Value<float>("b") : 1f,
                    color["a"] != null ? color.Value<float>("a") : 1f
                );
        }

        private static bool HasMultipleAsepriteFrames(AssetImporter importer)
        {
            var serializedObject = new SerializedObject(importer);
            var frames = serializedObject.FindProperty("m_AnimatedSpriteImportData");

            if (frames != null && frames.isArray && frames.arraySize > 1)
                return true;

            var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(importer.assetPath);
            var spriteCount = 0;

            foreach (var asset in assets)
            {
                if (asset is Sprite)
                    spriteCount++;
            }

            return spriteCount > 1;
        }
    }
}