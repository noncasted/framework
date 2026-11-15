using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // A ContentReference<T> as an object field of T under the label of the reference: the field writes the GUID and the
    // local id of the object it is given (ContentReferenceFields).
    [CustomPropertyDrawer(typeof(ContentReference<>))]
    public sealed class ContentReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var type = TargetType();
            var current = ContentReferenceFields.Target(property);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var picked = EditorGUI.ObjectField(position, label, current, type, false);

            if (EditorGUI.EndChangeCheck())
            {
                ContentReferenceFields.AssignObject(property, picked);
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        // T of the field: of the list element when the field is a list of references.
        private Type TargetType()
        {
            var type = fieldInfo.FieldType;

            if (type.IsArray)
                type = type.GetElementType();
            else if (type.IsGenericType && type.GetGenericTypeDefinition() != typeof(ContentReference<>))
                type = type.GetGenericArguments()[0];

            return type != null && type.IsGenericType ? type.GetGenericArguments()[0] : typeof(Object);
        }
    }
}
