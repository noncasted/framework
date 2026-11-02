using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Internal
{
    // The YAML of the games holds the old keys: FormerlySerializedAs stays until every game has re-saved its assets.
    // The keys are for SerializedProperty access from the generators and the tests.
    [Serializable]
    public sealed class PrefabAssetEntry
    {
        public const string NameKey = nameof(_name);
        public const string PrefabKey = nameof(_prefab);
        public const string ComponentTypeKey = nameof(_componentType);

        [SerializeField] [FormerlySerializedAs("Name")] private string _name;
        [SerializeField] [FormerlySerializedAs("Prefab")] private GameObject _prefab;
        [SerializeField] [FormerlySerializedAs("ComponentType")] private string _componentType;

        public string Name => _name;
        public GameObject Prefab => _prefab;
        public string ComponentType => _componentType;
    }

    public sealed class PrefabGroupAsset : ScriptableObject
    {
        [SerializeField] private PrefabAssetEntry[] _entries = Array.Empty<PrefabAssetEntry>();

        public PrefabEntry Get(string name)
        {
            var entry = FindEntry(name);

            if (entry == null || entry.Prefab == null)
                throw new InvalidOperationException($"Prefab '{name}' is missing in {this.name}");

            ValidateComponent(entry.Prefab, entry.ComponentType);
            return new PrefabEntry(entry.Prefab);
        }

        public T Get<T>(string name) where T : Component
        {
            return Get(name).As<T>();
        }

        public GameObject GetGameObject(string name)
        {
            return Get(name).Asset;
        }

        private PrefabAssetEntry FindEntry(string name)
        {
            return GroupEntries.Find(_entries, name, static entry => entry.Name);
        }

        private static void ValidateComponent(GameObject prefab, string componentType)
        {
            if (string.IsNullOrEmpty(componentType))
                return;

            if (HasComponent(prefab, componentType))
                return;

            throw new InvalidOperationException($"{prefab.name} has no {componentType}");
        }

        private static bool HasComponent(GameObject prefab, string componentType)
        {
            var type = Type.GetType(componentType);

            if (type != null)
                return prefab.GetComponent(type) != null;

            var components = prefab.GetComponents<Component>();

            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];

                if (component == null)
                    continue;

                var actual = component.GetType();

                if (string.Equals(actual.AssemblyQualifiedName, componentType, StringComparison.Ordinal))
                    return true;

                var shortName = actual.FullName + ", " + actual.Assembly.GetName().Name;

                if (string.Equals(shortName, componentType, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}