using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Internal
{
    // The YAML of the games holds the old keys: FormerlySerializedAs stays until every game has re-saved its assets.
    // The keys are for SerializedProperty access from the generators and the tests.
    [Serializable]
    public sealed class ColorEntry
    {
        public const string NameKey = nameof(_name);
        public const string ValueKey = nameof(_value);

        [SerializeField] [FormerlySerializedAs("Name")] private string _name;
        [SerializeField] [FormerlySerializedAs("Value")] private Color _value = Color.white;

        // The serializer of Unity creates an entry through this one, which keeps the initial color.
        public ColorEntry()
        {
        }

        public ColorEntry(string name, Color value)
        {
            _name = name;
            _value = value;
        }

        public string Name => _name;
        public Color Value => _value;
    }

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

    [CreateAssetMenu(fileName = "ColorCatalog", menuName = "Tools/Color Catalog")]
    public sealed class ColorCatalog : ScriptableObject
    {
        [SerializeField] private List<ColorGroup> _groups = new();

        public IReadOnlyList<ColorGroup> Groups => _groups;

#if UNITY_EDITOR
        [Button("Refresh")]
        private void Refresh()
        {
            // FrameworkMenu.Codegen + "Colors" (ColorGenerator.Generate): the runtime assembly has no editor constants.
            EditorApplication.ExecuteMenuItem("Tools/Framework/Codegen/Colors");
        }
#endif
    }
}