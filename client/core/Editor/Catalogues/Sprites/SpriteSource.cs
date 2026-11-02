using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    internal sealed class SpriteSource
    {
        public SpriteSource(
            string path,
            string group,
            string propertyName,
            SpriteKind kind,
            bool isMultipleAnimations,
            SpriteCatalogMetadata metadata,
            float time,
            Color color,
            int priority)
        {
            Path = path;
            Group = group;
            PropertyName = propertyName;
            Kind = kind;
            IsMultipleAnimations = isMultipleAnimations;
            Metadata = metadata;
            Time = time;
            Color = color;
            Priority = priority;
        }

        public string Path { get; }
        public string Group { get; }
        public string PropertyName { get; }
        public SpriteKind Kind { get; }
        public bool IsMultipleAnimations { get; }
        public SpriteCatalogMetadata Metadata { get; }
        public List<BakedAnimation> BakedAnimations { get; private set; } = new();
        public float Time { get; }
        public Color Color { get; }
        public int Priority { get; }

        public void SetBakedAnimations(List<BakedAnimation> animations)
        {
            BakedAnimations = animations;
        }
    }
}
