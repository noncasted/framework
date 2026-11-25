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
}
