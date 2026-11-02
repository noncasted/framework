using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // The text form of a prefab hierarchy for agents: objects, components, the visible fields of the behaviours.
    internal static class PrefabHierarchyDump
    {
        public static string Build(GameObject obj, int depth)
        {
            var sb = new StringBuilder();
            var indent = new string(' ', depth * 2);
            var active = obj.activeSelf ? "" : " [INACTIVE]";
            sb.AppendLine($"{indent}- {obj.name}{active}");

            var components = obj.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null)
                {
                    sb.AppendLine($"{indent}  [MISSING SCRIPT]");
                    continue;
                }

                var typeName = comp.GetType().Name;
                if (typeName == "Transform" || typeName == "RectTransform") continue;

                var details = GetComponentDetails(comp, depth);
                sb.AppendLine($"{indent}  [{typeName}]{details}");
            }

            for (int i = 0; i < obj.transform.childCount; i++)
            {
                var child = obj.transform.GetChild(i).gameObject;
                sb.Append(Build(child, depth + 1));
            }

            return sb.ToString();
        }

        private static string GetComponentDetails(Component comp, int depth)
        {
            var sb = new StringBuilder();

            if (comp is MonoBehaviour mb)
            {
                var so = new SerializedObject(mb);
                var prop = so.GetIterator();
                var fields = new List<string>();
                prop.NextVisible(true);
                while (prop.NextVisible(false))
                {
                    if (prop.name == "m_Script") continue;
                    var val = GetPropertyValue(prop);
                    if (val != null)
                        fields.Add($"{prop.name}={val}");
                }

                if (fields.Count > 0)
                    sb.Append($" ({string.Join(", ", fields)})");
            }

            return sb.ToString();
        }

        private static string GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F2");
                case SerializedPropertyType.String:
                    return string.IsNullOrEmpty(prop.stringValue) ? null : $"\"{prop.stringValue}\"";
                // A mixed value of a [Flags] enum has no index.
                case SerializedPropertyType.Enum:
                    return prop.enumValueIndex >= 0 ? prop.enumDisplayNames[prop.enumValueIndex] : prop.intValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "None";
                case SerializedPropertyType.Color:
                    var c = prop.colorValue;
                    return $"({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2})";
                case SerializedPropertyType.Vector2:
                    var v2 = prop.vector2Value;
                    return $"({v2.x:F1},{v2.y:F1})";
                case SerializedPropertyType.Vector3:
                    var v3 = prop.vector3Value;
                    return $"({v3.x:F1},{v3.y:F1},{v3.z:F1})";
                default:
                    return null;
            }
        }
    }
}
