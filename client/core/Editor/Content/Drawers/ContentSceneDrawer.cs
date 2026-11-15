using UnityEditor;
using UnityEngine;

namespace Internal
{
    // A ContentScene as a scene field under the label of the scene: the field writes the GUID of the scene it is given.
    [CustomPropertyDrawer(typeof(ContentScene))]
    public sealed class ContentSceneDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var current = ContentReferenceFields.TargetScene(property);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var picked = (SceneAsset)EditorGUI.ObjectField(position, label, current, typeof(SceneAsset), false);

            if (EditorGUI.EndChangeCheck())
            {
                ContentReferenceFields.AssignScene(property, picked == null ? null : AssetDatabase.GetAssetPath(picked));
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
