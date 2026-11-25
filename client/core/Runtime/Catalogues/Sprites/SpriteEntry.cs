using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Internal
{
    // The YAML of the games holds the old keys: FormerlySerializedAs stays until every game has re-saved its assets.
    // The keys are for SerializedProperty access from the generators and the tests.
    [Serializable]
    public sealed class SpriteEntry
    {
        public const string NameKey = nameof(_name);
        public const string KindKey = nameof(_kind);
        public const string SpritesKey = nameof(_sprites);
        public const string TimeKey = nameof(_time);
        public const string ColorKey = nameof(_color);

        [SerializeField] [FormerlySerializedAs("Name")] private string _name;
        [SerializeField] [FormerlySerializedAs("Kind")] private SpriteKind _kind;
        [SerializeField] [FormerlySerializedAs("Sprites")] private Sprite[] _sprites;
        [SerializeField] [FormerlySerializedAs("Time")] private float _time;
        [SerializeField] [FormerlySerializedAs("Color")] private Color _color;

        public string Name => _name;
        public SpriteKind Kind => _kind;
        public Sprite[] Sprites => _sprites;
        public float Time => _time;
        public Color Color => _color;
    }
}
