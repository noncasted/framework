using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    public sealed class SpriteGroupAsset : ScriptableObject
    {
        [SerializeField] private SpriteEntry[] _entries;

        // Built once per load: the entries of a loaded group do not change at runtime. The editor rewrites them through
        // the generator or a serialized object, and OnValidate drops the cache.
        [NonSerialized] private List<Sprite> _sheets;

        // Every sheet of the group in the order of its names: the generated properties are sorted, so an index in a
        // name (Icon_000) keeps its place when a new sprite is added.
        public IReadOnlyList<Sprite> Sheets => _sheets ??= CollectSheets();

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

        private void OnValidate()
        {
            _sheets = null;
        }

        private List<Sprite> CollectSheets()
        {
            if (_entries == null)
                throw new InvalidOperationException($"{name} has no entries: the group asset is not generated");

            var sorted = new List<SpriteEntry>();

            foreach (var entry in _entries)
            {
                if (entry.Kind != SpriteKind.Sheet)
                    continue;

                if (entry.Sprites == null || entry.Sprites.Length == 0 || entry.Sprites[0] == null)
                    throw new InvalidOperationException($"Sheet '{entry.Name}' of {name} has no sprite");

                sorted.Add(entry);
            }

            sorted.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));

            var sheets = new List<Sprite>(sorted.Count);

            foreach (var entry in sorted)
                sheets.Add(entry.Sprites[0]);

            return sheets;
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