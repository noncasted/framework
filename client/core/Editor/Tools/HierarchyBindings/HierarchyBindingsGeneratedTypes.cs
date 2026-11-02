using System;
using UnityEngine;

namespace Internal
{
    // The class the generator writes: its name and the way from a component on an object to it.
    internal static class HierarchyBindingsGeneratedTypes
    {
        public const string TypeNameSuffix = "Bindings";

        public static string ToTypeName(string bindingsName)
        {
            var identifier = HierarchyBindingsNaming.ToIdentifier(bindingsName);

            if (string.IsNullOrEmpty(identifier))
                return string.Empty;

            if (identifier.EndsWith(TypeNameSuffix, StringComparison.Ordinal))
                return identifier;

            return identifier + TypeNameSuffix;
        }

        // The user class inherits the generated one, so from the component on the object we
        // have to walk up to the type directly under ObjectBindings - that is the generated one.
        public static Type ResolveGeneratedType(Component bindings)
        {
            if (bindings == null)
                return null;

            var type = bindings.GetType();

            while (type != null && type.BaseType != typeof(ObjectBindings))
                type = type.BaseType;

            return type;
        }

        // The bindings of this class on the object, if any: the existing component may be a user class
        // that inherits the generated one.
        public static ObjectBindings FindBindings(GameObject root, string typeName, string namespaceName)
        {
            foreach (var bindings in root.GetComponents<ObjectBindings>())
            {
                if (IsGeneratedAs(bindings, typeName, namespaceName))
                    return bindings;
            }

            return null;
        }

        public static bool IsGeneratedAs(ObjectBindings bindings, string typeName, string namespaceName)
        {
            var type = ResolveGeneratedType(bindings);

            return type != null &&
                   string.Equals(type.Name, typeName, StringComparison.Ordinal) &&
                   string.Equals(type.Namespace ?? string.Empty, namespaceName, StringComparison.Ordinal);
        }
    }
}
