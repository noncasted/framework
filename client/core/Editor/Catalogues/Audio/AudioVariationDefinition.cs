using System.Collections.Generic;

namespace Internal
{
    internal sealed class AudioVariationDefinition
    {
        private readonly List<AudioPropertyDefinition> _entries = new();

        public AudioVariationDefinition(string propertyName, string fieldName)
        {
            PropertyName = propertyName;
            FieldName = fieldName;
        }

        public string PropertyName { get; }
        public string FieldName { get; }
        public IReadOnlyList<AudioPropertyDefinition> Entries => _entries;

        public void AddEntry(AudioPropertyDefinition entry)
        {
            _entries.Add(entry);
        }

        public void SortEntries()
        {
            _entries.Sort((left, right) => left.Variation.Index.CompareTo(right.Variation.Index));
        }
    }
}
