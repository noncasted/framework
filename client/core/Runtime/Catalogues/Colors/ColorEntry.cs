using System;
using UnityEngine;
using UnityEngine.Serialization;

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
}
