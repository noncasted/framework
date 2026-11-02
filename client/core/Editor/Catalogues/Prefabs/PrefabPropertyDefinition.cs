using UnityEngine;

namespace Internal
{
    internal sealed class PrefabPropertyDefinition
    {
        public PrefabPropertyDefinition(
            string propertyName,
            string fieldName,
            GameObject prefab,
            string componentType,
            string componentDisplayName)
        {
            PropertyName = propertyName;
            FieldName = fieldName;
            Prefab = prefab;
            ComponentType = componentType;
            ComponentDisplayName = componentDisplayName;
        }

        public string PropertyName { get; }
        public string FieldName { get; }
        public GameObject Prefab { get; }
        public string ComponentType { get; }
        public string ComponentDisplayName { get; }
        public bool IsGameObject { get; private set; }
        public string TypeFullName { get; private set; }
        public string TypeAssembly { get; private set; }
        public string CodeTypeName { get; private set; }

        public void SetGameObjectType()
        {
            IsGameObject = true;
            TypeFullName = "UnityEngine.GameObject";
            TypeAssembly = "UnityEngine";
            CodeTypeName = "global::UnityEngine.GameObject";
        }

        public void SetComponentType(string typeFullName, string typeAssembly)
        {
            IsGameObject = false;
            TypeFullName = typeFullName;
            TypeAssembly = typeAssembly;
            CodeTypeName = "global::" + typeFullName;
        }
    }
}
