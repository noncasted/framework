using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // Mirror of one hierarchy object: components become fields, children become nested classes.
    internal sealed class HierarchyBindingsNode
    {
        public HierarchyBindingsNode(
            GameObject target,
            string typeName,
            string hierarchyPath,
            IdentifierScope members,
            bool ignoreChildren)
        {
            Target = target;
            TypeName = typeName;
            HierarchyPath = hierarchyPath;
            Members = members;
            IgnoreChildren = ignoreChildren;
        }

        public GameObject Target { get; }
        public string HierarchyPath { get; }

        // Nested class name; for the root it is the name of the generated MonoBehaviour itself.
        public string TypeName { get; private set; }

        // Empty for the root: it is not a field of anyone.
        public string PropertyName { get; private set; }
        public string FieldName { get; private set; }

        // The names of the class: its members and nested classes; its own type name must differ from all of them.
        public IdentifierScope Members { get; }

        // The object has HierarchyBindingsIgnoreChildren: the children did not get into the mirror.
        public bool IgnoreChildren { get; }

        // Consecutive identical siblings are collapsed into an array. This node is the first of them and serves
        // as the template: the class is generated from it, and references are filled for each element.
        // Empty for a single child.
        public readonly List<HierarchyBindingsNode> Elements = new();

        public bool IsArray => Elements.Count > 0;

        public readonly List<HierarchyBindingsField> Fields = new();
        public readonly List<HierarchyBindingsNode> Children = new();

        // A child is named by its parent once it is known what collapses into what.
        public void SetNames(string typeName, string propertyName, string fieldName)
        {
            TypeName = typeName;
            PropertyName = propertyName;
            FieldName = fieldName;
        }
    }
}
