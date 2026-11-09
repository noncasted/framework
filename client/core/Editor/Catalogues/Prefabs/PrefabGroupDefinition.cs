using System;
using System.Collections.Generic;

namespace Internal
{
    internal sealed class PrefabGroupDefinition : ICatalogGroupDefinition
    {
        private readonly List<PrefabPropertyDefinition> _properties = new();

        public PrefabGroupDefinition(string name, string className, bool isPreloaded)
        {
            Name = name;
            ClassName = className;
            IsPreloaded = isPreloaded;
        }

        public string Name { get; }
        public string Guid { get; private set; }
        public string ClassName { get; }
        public string GeneratedFolder { get; private set; }
        public bool SkipCodegen { get; private set; }
        public bool IsPreloaded { get; }
        public IReadOnlyList<PrefabPropertyDefinition> Properties => _properties;

        public void SetGuid(string guid)
        {
            Guid = guid;
        }

        public void SetGeneratedFolder(string generatedFolder)
        {
            GeneratedFolder = generatedFolder;
        }

        public void DisableCodegen()
        {
            SkipCodegen = true;
        }

        public void AddProperty(PrefabPropertyDefinition property)
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
