using System;
using System.Collections.Generic;

namespace Internal
{
    internal sealed class AssetGroupDefinition : ICatalogGroupDefinition
    {
        // The folder of the group assets inside Resources.
        public const string ResourcesSubfolder = "AssetGroups";

        private readonly List<AssetPropertyDefinition> _properties = new();

        public AssetGroupDefinition(string name, string className, bool isAddressable, string resourcePath)
        {
            Name = name;
            ClassName = className;
            IsAddressable = isAddressable;
            ResourcePath = resourcePath;
        }

        public string Name { get; }
        public string Address { get; private set; }
        public string ClassName { get; }
        public string TargetNamespace { get; private set; }
        public string GeneratedFolder { get; private set; }
        public bool SkipCodegen { get; private set; }
        public bool IsAddressable { get; }
        public string ResourcePath { get; }
        public IReadOnlyList<AssetPropertyDefinition> Properties => _properties;

        public void SetAddress(string address)
        {
            Address = address;
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
