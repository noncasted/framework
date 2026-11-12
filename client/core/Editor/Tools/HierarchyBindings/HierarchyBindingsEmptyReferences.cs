using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // The generator fills every reference of the class it writes, elements of arrays included, so an empty one means
    // the object or the component it pointed at is gone: replaced by another object, removed in a variant, deleted.
    internal static class HierarchyBindingsEmptyReferences
    {
        private const BindingFlags DeclaredFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // empty: the property paths of the empty references ('_tiles.Array.data[3]'); unserialized: the fields of
        // the class the object does not hold (the domain and the asset are of different versions of the code).
        public static void Collect(
            ObjectBindings bindings,
            Type generatedType,
            List<string> empty,
            List<string> unserialized)
        {
            var serializedObject = new SerializedObject(bindings);

            foreach (var field in generatedType.GetFields(DeclaredFields))
            {
                if (field.IsDefined(typeof(SerializeField)) == false)
                    continue;

                var property = serializedObject.FindProperty(field.Name);

                if (property == null)
                {
                    unserialized.Add(field.Name);
                    continue;
                }

                Collect(property, empty);
            }
        }

        private static void Collect(SerializedProperty root, List<string> empty)
        {
            // The walk is limited to the subtree of the field: everything under it has a path with its prefix.
            var current = root.Copy();
            var prefix = root.propertyPath;

            do
            {
                if (current.propertyPath != prefix &&
                    current.propertyPath.StartsWith(prefix + ".", StringComparison.Ordinal) == false)
                    break;

                if (current.propertyType == SerializedPropertyType.ObjectReference &&
                    current.objectReferenceValue == null)
                    empty.Add(current.propertyPath);
            }
            while (current.Next(true));
        }
    }
}
