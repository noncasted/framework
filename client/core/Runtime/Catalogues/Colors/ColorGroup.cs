using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Internal
{
    // The YAML of the games holds the old keys: FormerlySerializedAs stays until every game has re-saved its assets.
    [Serializable]
    public sealed class ColorGroup
    {
        public const string NameKey = nameof(_name);
        public const string EntriesKey = nameof(_entries);

        [SerializeField] [FormerlySerializedAs("Name")] private string _name;
        [SerializeField] [FormerlySerializedAs("Entries")] private List<ColorEntry> _entries = new();

        // The serializer of Unity creates a group through this one, which keeps the list of an empty group.
        public ColorGroup()
        {
        }

        public ColorGroup(string name, List<ColorEntry> entries)
        {
            _name = name;
            _entries = entries;
        }

        public string Name => _name;
        public IReadOnlyList<ColorEntry> Entries => _entries;
    }
}
