using UnityEngine;

namespace Internal
{
    internal sealed class PrefabSource
    {
        public PrefabSource(string path, string group, string propertyName, GameObject prefab, string componentType)
        {
            Path = path;
            Group = group;
            PropertyName = propertyName;
            Prefab = prefab;
            ComponentType = componentType;
        }

        public string Path { get; }
        public string Group { get; }
        public string PropertyName { get; }
        public GameObject Prefab { get; }
        public string ComponentType { get; }
    }
}
