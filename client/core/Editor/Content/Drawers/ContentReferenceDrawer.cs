using UnityEditor;
using UnityEngine;

namespace Internal
{
    // A ContentReference<T> as an object field of T: the field is the engine's own drawer of its Loadable, drawn under the
    // label of the reference; the GUID and the local id follow the id after every draw (an edit, an undo, a paste).
    [CustomPropertyDrawer(typeof(ContentReference<>))]
    public sealed class ContentReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var loadable = property.FindPropertyRelative(ContentReferenceFields.Loadable);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.PropertyField(position, loadable, label);
            EditorGUI.EndProperty();

            if (ContentReferenceFields.SyncObject(property))
                property.serializedObject.ApplyModifiedProperties();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property.FindPropertyRelative(ContentReferenceFields.Loadable), label);
        }
    }
}
