using System;
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
