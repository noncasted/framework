using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Internal
{
    // The YAML of the games holds the old keys: FormerlySerializedAs stays until every game has re-saved its assets.
    // The keys are for SerializedProperty access from the generators and the tests.
    [Serializable]
    public sealed class EnvAssetEntry
    {
        public const string NameKey = nameof(_name);
        public const string AssetKey = nameof(_asset);

        [SerializeField] [FormerlySerializedAs("Name")] private string _name;
        [SerializeField] [FormerlySerializedAs("Asset")] private EnvAsset _asset;

        public string Name => _name;
        public EnvAsset Asset => _asset;
    }

    public sealed class EnvAssetGroupAsset : ScriptableObject
    {
        [SerializeField] private EnvAssetEntry[] _entries = Array.Empty<EnvAssetEntry>();

        // Every asset of the group of one type: for lookups that are built from all the assets of a kind (node styles
        // by key) instead of the generated properties.
        public IReadOnlyList<T> GetAll<T>() where T : EnvAsset
        {
            var assets = new List<T>();

            foreach (var entry in _entries)
            {
                if (entry.Asset == null)
                    throw new InvalidOperationException($"{this.name} has an empty entry '{entry.Name}'");

                if (entry.Asset is T typed)
                    assets.Add(typed);
            }

            return assets;
        }

        public T Get<T>(string name) where T : EnvAsset
        {
            var entry = FindEntry(name);

            if (entry == null || entry.Asset == null)
                throw new InvalidOperationException($"Asset '{name}' is missing in {this.name}");

            if (entry.Asset is not T typed)
                throw new InvalidOperationException(
                    $"Asset '{name}' in {this.name} is {entry.Asset.GetType().Name}, expected {typeof(T).Name}");

            return typed;
        }

        private EnvAssetEntry FindEntry(string name)
        {
            return GroupEntries.Find(_entries, name, static entry => entry.Name);
        }
    }
}
