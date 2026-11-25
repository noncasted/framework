using System;

namespace Internal
{
    // A binding request survives the domain reload: first we write the code, after compilation
    // we return to the same object and fill the references.
    [Serializable]
    internal sealed class HierarchyBindingsRequest
    {
        public string AssetPath;

        // Sibling indices from the root of the asset ("1/0"; for a scene the first one is the index among the root
        // objects of the scene; empty - the root itself). Names are not unique and may contain '/'.
        public string ObjectPath;

        // The names of the same path, for messages only.
        public string ObjectNames;

        public string TypeName;
        public string Namespace;
        public bool IsPrefabAsset;
        public bool IsSceneService;
        public bool IsEntityComponent;

        // Hash of the structure the code was generated from. The binding phase scans the object again and must get
        // the same structure.
        public string StructureHash;
    }
}