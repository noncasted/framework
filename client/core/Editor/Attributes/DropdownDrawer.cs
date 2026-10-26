using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [CustomPropertyDrawer(typeof(DropdownAttribute))]
    public sealed class DropdownDrawer : PropertyDrawer
    {
        // DeclaredOnly plus a manual walk up the base types: private members of the bases are not visible otherwise.
        private const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // The options may be read from disk, so they are reread only when the list opens.
        private List<DropdownOption> _options;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // A negative height cancels the spacing Unity leaves after a hidden field.
            if (IsVisible(property) == false)
                return -EditorGUIUtility.standardVerticalSpacing;

            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            if (IsVisible(property) == false)
                return;

            if (property.propertyType != SerializedPropertyType.String)
                throw new InvalidOperationException($"{nameof(DropdownAttribute)} works only with string fields: {property.propertyPath}");

            _options ??= ReadOptions(property);

            EditorGUI.BeginProperty(rect, label, property);

            var buttonRect = EditorGUI.PrefixLabel(rect, label);
            var current = property.stringValue;

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(FindLabel(current)), FocusType.Keyboard))
            {
                _options = ReadOptions(property);
                ShowMenu(property, current);
            }

            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }

        private void ShowMenu(SerializedProperty property, string current)
        {
            var menu = new GenericMenu();

            foreach (var option in _options)
            {
                var value = option.Value;
                menu.AddItem(new GUIContent(option.Label), value == current, () => Assign(property, value));
            }

            menu.ShowAsContext();
        }

        private static void Assign(SerializedProperty property, string value)
        {
            property.serializedObject.Update();
            property.stringValue = value;
            property.serializedObject.ApplyModifiedProperties();
        }

        // A value missing from the options is shown as it is.
        private string FindLabel(string value)
        {
            foreach (var option in _options)
            {
                if (option.Value == value)
                    return option.Label;
            }

            return value;
        }

        private List<DropdownOption> ReadOptions(SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;
            var name = ((DropdownAttribute)attribute).OptionsMethod;

            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                var method = type.GetMethod(name, MemberFlags, null, Type.EmptyTypes, null);

                if (method != null)
                    return new List<DropdownOption>((IEnumerable<DropdownOption>)method.Invoke(target, null));
            }

            throw new MissingMethodException(target.GetType().FullName, name);
        }

        private bool IsVisible(SerializedProperty property)
        {
            var name = ((DropdownAttribute)attribute).VisibleIf;

            if (name == null)
                return true;

            var target = property.serializedObject.targetObject;

            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                var member = type.GetProperty(name, MemberFlags);

                if (member != null)
                    return (bool)member.GetValue(target);

                var field = type.GetField(name, MemberFlags);

                if (field != null)
                    return (bool)field.GetValue(target);
            }

            throw new MissingMemberException(target.GetType().FullName, name);
        }
    }
}
