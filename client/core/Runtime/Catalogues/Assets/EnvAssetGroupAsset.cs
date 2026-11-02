using System;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public sealed class EnvAssetEntry
    {
        public string Name;
        public EnvAsset Asset;
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
