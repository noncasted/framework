using System.Collections.Generic;

namespace Internal
{
    // Field referencing a component. Boundary objects (own bindings, nested prefab) also end up
    // here: from outside they look like a regular reference, the generator does not go inside.
    internal sealed class HierarchyBindingsField
    {
        public HierarchyBindingsField(
            UnityEngine.Object target,
            string propertyName,
            string fieldName,
            string codeTypeName,
            string comment = null)
        {
            Target = target;
            PropertyName = propertyName;
            FieldName = fieldName;
            CodeTypeName = codeTypeName;
            Comment = comment;
        }

        public UnityEngine.Object Target { get; }

        // Collapsed group of boundary siblings: the field becomes an array, and Target is the first element.
        public readonly List<UnityEngine.Object> Targets = new();

        public string PropertyName { get; }
        public string FieldName { get; }
        public string CodeTypeName { get; }
        public string Comment { get; private set; }

        public bool IsArray => Targets.Count > 0;

        public string DeclaredTypeName => IsArray ? CodeTypeName + "[]" : CodeTypeName;

        public void SetComment(string comment)
        {
            Comment = comment;
        }
    }
}
