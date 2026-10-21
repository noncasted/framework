using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // Mirror of one hierarchy object: components become fields, children become nested classes.
    internal sealed class HierarchyBindingsNode
    {
        public GameObject Target;
        public string HierarchyPath;

        // Nested class name; for the root it is the name of the generated MonoBehaviour itself.
        public string TypeName;

        // Empty for the root: it is not a field of anyone.
        public string PropertyName;
        public string FieldName;

        // The object has HierarchyBindingsIgnoreChildren: the children did not get into the mirror.
        public bool IgnoreChildren;

        // Consecutive identical siblings are collapsed into an array. This node is the first of them and serves
        // as the template: the class is generated from it, and references are filled for each element.
        // Empty for a single child.
        public readonly List<HierarchyBindingsNode> Elements = new();

        public bool IsArray => Elements.Count > 0;

        public readonly List<HierarchyBindingsField> Fields = new();
        public readonly List<HierarchyBindingsNode> Children = new();
    }

    // Field referencing a component. Boundary objects (own bindings, nested prefab) also end up
    // here: from outside they look like a regular reference, the generator does not go inside.
    internal sealed class HierarchyBindingsField
    {
        public UnityEngine.Object Target;

        // Collapsed group of boundary siblings: the field becomes an array, and Target is the first element.
        public readonly List<UnityEngine.Object> Targets = new();

        public string PropertyName;
        public string FieldName;
        public string CodeTypeName;
        public string Comment;

        public bool IsArray => Targets.Count > 0;

        public string DeclaredTypeName => IsArray ? CodeTypeName + "[]" : CodeTypeName;
    }
}
