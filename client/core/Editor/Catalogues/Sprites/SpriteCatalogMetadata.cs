using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    public sealed class SpriteCatalogMetadata
    {
        public const float DefaultTime = 0.8f;
        public const float DefaultFramesPerSecond = 12f;
        public static readonly Color DefaultColor = Color.white;

        private const string UserDataKey = "spriteCatalog";
        private const string LogTag = "SpriteCatalogMetadata";

        public SpriteCatalogMetadata()
        {
        }

        internal SpriteCatalogMetadata(
            bool included,
            string group,
            SpriteCatalogKind kind,
            float time,
            Color color,
            float framesPerSecond,
            List<SpriteCatalogAnimation> animations,
            List<SpriteCatalogCustomAnimation> customAnimations,
            List<SpriteCatalogTagSpeed> tagSpeeds)
        {
            Included = included;
            Group = group;
            Kind = kind;
            Time = time;
            Color = color;
            FramesPerSecond = framesPerSecond;
            _animations = animations;
            _customAnimations = customAnimations;
            _tagSpeeds = tagSpeeds;
        }

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
            metadata = SpriteCatalogMetadataJson.Read(catalog);
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
        public static void Write(AssetImporter importer, SpriteCatalogMetadata metadata)
        {
            if (AssetUserData.Write(importer, UserDataKey, LogTag, SpriteCatalogMetadataJson.Write(metadata)))
                SpriteGroupsRegistry.Instance.Invalidate();
        }

        // Change from the inspector: stays in the importer's memory until Apply.
        public static void Stage(AssetImporter importer, SpriteCatalogMetadata metadata)
        {
            if (AssetUserData.Stage(importer, UserDataKey, LogTag, SpriteCatalogMetadataJson.Write(metadata)))
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
