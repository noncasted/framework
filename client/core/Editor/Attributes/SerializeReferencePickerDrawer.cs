using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [CustomPropertyDrawer(typeof(SerializeReferencePickerAttribute))]
    public sealed class SerializeReferencePickerDrawer : PropertyDrawer
    {
        private const string NullName = "None";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, property);

            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(rect, property, label, true);
                EditorGUI.EndProperty();
                return;
            }

            var headerRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            var buttonRect = EditorGUI.PrefixLabel(headerRect, GUIUtility.GetControlID(FocusType.Passive), GUIContent.none);
            buttonRect.xMin = rect.x + EditorGUIUtility.labelWidth + 2f;

            var current = property.managedReferenceValue?.GetType();

            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(current?.Name ?? NullName), FocusType.Keyboard))
                ShowMenu(property, current);

            // Fields of the selected type are drawn under a foldout header.
            EditorGUI.PropertyField(rect, property, label, true);
            EditorGUI.EndProperty();
        }

        private static void ShowMenu(SerializedProperty property, Type current)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent(NullName), current == null, () => Assign(property, null));

            foreach (var type in CollectTypes(property))
                menu.AddItem(new GUIContent(type.Name), type == current, () => Assign(property, type));

            menu.ShowAsContext();
        }

        private static void Assign(SerializedProperty property, Type type)
        {
            property.serializedObject.Update();
            property.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            property.serializedObject.ApplyModifiedProperties();
        }

        private static List<Type> CollectTypes(SerializedProperty property)
        {
            var result = new List<Type>();
            var baseType = ResolveFieldType(property.managedReferenceFieldTypename);

            if (baseType == null)
                return result;

            foreach (var type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (type.IsAbstract || type.IsGenericType || type.IsSerializable == false)
                    continue;

                if (typeof(UnityEngine.Object).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                result.Add(type);
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            return result;
        }

        // Unity format: "<assembly> <full type name>".
        private static Type ResolveFieldType(string typename)
        {
            var separator = typename.IndexOf(' ');

            if (separator < 0)
                return null;

            var assembly = typename.Substring(0, separator);
            var fullName = typename.Substring(separator + 1);

            return Type.GetType($"{fullName}, {assembly}");
        }
    }
}
