using System;
using System.Collections.Generic;

namespace Internal
{
    internal sealed class AudioGroupDefinition : ICatalogGroupDefinition
    {
        private readonly List<AudioPropertyDefinition> _properties = new();
        private readonly List<AudioVariationDefinition> _variations = new();

        public AudioGroupDefinition(string name, string className)
        {
            Name = name;
            ClassName = className;
        }

        public string Name { get; }
        public string Guid { get; private set; }
        public string ClassName { get; }
        public IReadOnlyList<AudioPropertyDefinition> Properties => _properties;
        public IReadOnlyList<AudioVariationDefinition> Variations => _variations;

        public void SetGuid(string guid)
        {
            Guid = guid;
        }

        public void AddProperty(AudioPropertyDefinition property)
        {
            _properties.Add(property);
        }

        public void SortProperties()
        {
            _properties.Sort((left, right) =>
                string.Compare(left.PropertyName, right.PropertyName, StringComparison.Ordinal));
        }

        public void AddVariation(AudioVariationDefinition variation)
        {
            _variations.Add(variation);
        }

        public void SortVariations()
        {
            _variations.Sort((left, right) =>
                string.Compare(left.PropertyName, right.PropertyName, StringComparison.Ordinal));
        }
    }
}
