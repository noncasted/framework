using UnityEditor;
using UnityEngine;

namespace Internal
{
    // A ContentScene as a scene field: the engine's own drawer of its LoadableSceneId under the label of the scene; the
    // GUID follows the id after every draw.
    [CustomPropertyDrawer(typeof(ContentScene))]
    public sealed class ContentSceneDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var id = property.FindPropertyRelative(ContentReferenceFields.SceneId);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.PropertyField(position, id, label);
            EditorGUI.EndProperty();

            if (ContentReferenceFields.SyncScene(property))
                property.serializedObject.ApplyModifiedProperties();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property.FindPropertyRelative(ContentReferenceFields.SceneId), label);
        }
    }
}
