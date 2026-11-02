using UnityEngine;

namespace Internal
{
    internal sealed class SpritePropertyDefinition
    {
        public SpritePropertyDefinition(
            string propertyName,
            string fieldName,
            SpriteKind kind,
            Sprite[] sprites,
            float time,
            Color color)
        {
            PropertyName = propertyName;
            FieldName = fieldName;
            Kind = kind;
            Sprites = sprites;
            Time = time;
            Color = color;
        }

        public string PropertyName { get; }
        public string FieldName { get; private set; }

        // The Multiple Animations file the animation belongs to, and its name inside that file.
        public string Owner { get; private set; }
        public string MemberName { get; private set; }
        public SpriteKind Kind { get; }
        public Sprite[] Sprites { get; }
        public float Time { get; }
        public Color Color { get; }

        public void NestInOwner(string owner, string memberName, string fieldName)
        {
            Owner = owner;
            MemberName = memberName;
            FieldName = fieldName;
        }
    }
}
