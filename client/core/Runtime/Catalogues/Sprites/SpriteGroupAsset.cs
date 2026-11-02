using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Internal
{
    public enum SpriteKind
    {
        Sheet,
        Animation
    }

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

    public sealed class SpriteGroupAsset : ScriptableObject
    {
        [SerializeField] private SpriteEntry[] _entries;

        public Sprite GetSheet(string name)
        {
            var entry = FindEntry(name, SpriteKind.Sheet);

            if (entry == null || entry.Sprites == null || entry.Sprites.Length == 0)
                throw new InvalidOperationException($"Sheet '{name}' is missing in {this.name}");

            return entry.Sprites[0];
        }

        public ISpriteAnimationData GetAnimation(string name)
        {
            var entry = FindEntry(name, SpriteKind.Animation);

            if (entry == null || entry.Sprites == null || entry.Sprites.Length == 0)
                throw new InvalidOperationException($"Animation '{name}' is missing in {this.name}");

            return new SpriteAnimationData(entry.Sprites, entry.Time, entry.Color);
        }

        public bool HasAnimation(string name)
        {
            var entry = FindEntry(name, SpriteKind.Animation);
            return entry != null && entry.Sprites != null && entry.Sprites.Length > 0;
        }

        private SpriteEntry FindEntry(string name, SpriteKind kind)
        {
            for (var i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];

                if (entry.Kind != kind)
                    continue;

                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }
    }
}