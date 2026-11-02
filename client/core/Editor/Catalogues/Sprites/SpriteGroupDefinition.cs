using System;
using System.Collections.Generic;

namespace Internal
{
    internal sealed class SpriteGroupDefinition : ICatalogGroupDefinition
    {
        private readonly List<SpritePropertyDefinition> _properties = new();

        public SpriteGroupDefinition(string name, string path, string className)
        {
            Name = name;
            Path = path;
            ClassName = className;
        }

        public string Name { get; }
        public string Address { get; private set; }
        public string Path { get; }
        public string ClassName { get; }
        public IReadOnlyList<SpritePropertyDefinition> Properties => _properties;

        public void SetAddress(string address)
        {
            Address = address;
        }

        public void AddProperty(SpritePropertyDefinition property)
        {
            _properties.Add(property);
        }

        public void SortProperties()
        {
            _properties.Sort((left, right) =>
                string.Compare(left.PropertyName, right.PropertyName, StringComparison.Ordinal));
        }
    }
}
