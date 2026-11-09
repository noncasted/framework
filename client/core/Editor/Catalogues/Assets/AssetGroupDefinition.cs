using System;
using System.Collections.Generic;

namespace Internal
{
    internal sealed class AssetGroupDefinition : ICatalogGroupDefinition
    {
        private readonly List<AssetPropertyDefinition> _properties = new();

        public AssetGroupDefinition(string name, string className, bool isPreloaded)
        {
            Name = name;
            ClassName = className;
            IsPreloaded = isPreloaded;
        }

        public string Name { get; }
        public string Guid { get; private set; }
        public string ClassName { get; }
        public string TargetNamespace { get; private set; }
        public string GeneratedFolder { get; private set; }
        public bool SkipCodegen { get; private set; }
        public bool IsPreloaded { get; }
        public IReadOnlyList<AssetPropertyDefinition> Properties => _properties;

        public void SetGuid(string guid)
        {
            Guid = guid;
        }

        public void SetGeneratedTarget(string targetNamespace, string generatedFolder)
        {
            TargetNamespace = targetNamespace;
            GeneratedFolder = generatedFolder;
        }

        public void DisableCodegen()
        {
            SkipCodegen = true;
        }

        public void AddProperty(AssetPropertyDefinition property)
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
