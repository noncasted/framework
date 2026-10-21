using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // A bar of three parts with two draggable borders; below it are the size fields of the first two.
    [NoAutoStaticsCleanup]
    [CustomPropertyDrawer(typeof(SplitSliderAttribute))]
    public sealed class SplitSliderDrawer : PropertyDrawer
    {
        private const float HandleWidth = 2f;
        private const float HandleHitWidth = 8f;

        private static readonly Color[] SegmentColors =
        {
            new(0.85f, 0.35f, 0.3f),
            new(0.9f, 0.7f, 0.25f),
            new(0.45f, 0.45f, 0.45f)
        };

        private static readonly int ControlHint = nameof(SplitSliderDrawer).GetHashCode();

        private static GUIStyle _segmentStyle;

        // Which border is being dragged: 0 - the first, 1 - the second.
        private static int _dragged;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var line = EditorGUIUtility.singleLineHeight;

            if (property.propertyType != SerializedPropertyType.Vector2)
                return line * 3f + EditorGUIUtility.standardVerticalSpacing;

            return line * 2f + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, property);

            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                DrawFallback(rect, property, label);
                EditorGUI.EndProperty();
                return;
            }

            var split = (SplitSliderAttribute)attribute;
            var line = EditorGUIUtility.singleLineHeight;

            var barRect = EditorGUI.PrefixLabel(new Rect(rect.x, rect.y, rect.width, line), label);
            var fieldsRect = new Rect(
                barRect.x,
                rect.y + line + EditorGUIUtility.standardVerticalSpacing,
                barRect.width,
                line);

            var value = Normalize(property.vector2Value);

            EditorGUI.BeginChangeCheck();

            value = DrawBar(barRect, value, split.Names, FindTotal(property, split));
            value = DrawFields(fieldsRect, value, split.Names);

            if (EditorGUI.EndChangeCheck())
                property.vector2Value = Normalize(value);

            EditorGUI.EndProperty();
        }

        private static Vector2 DrawBar(Rect rect, Vector2 value, string[] names, float? total)
        {
            var id = GUIUtility.GetControlID(ControlHint, FocusType.Passive, rect);
            var bounds = new[] { 0f, value.x, value.y, 1f };

            if (Event.current.type == EventType.Repaint)
            {
                for (var i = 0; i < 3; i++)
                {
                    var from = rect.x + rect.width * bounds[i];
                    var to = rect.x + rect.width * bounds[i + 1];
                    var segment = new Rect(from, rect.y, to - from, rect.height);

                    EditorGUI.DrawRect(segment, SegmentColors[i]);
                    DrawSegmentText(segment, names[i], bounds[i + 1] - bounds[i], total);
                }

                for (var i = 1; i <= 2; i++)
                {
                    var x = rect.x + rect.width * bounds[i] - HandleWidth * 0.5f;
                    EditorGUI.DrawRect(new Rect(x, rect.y, HandleWidth, rect.height), Color.white);
                }
            }

            for (var i = 1; i <= 2; i++)
            {
                var x = rect.x + rect.width * bounds[i];
                var hit = new Rect(x - HandleHitWidth * 0.5f, rect.y, HandleHitWidth, rect.height);
                EditorGUIUtility.AddCursorRect(hit, MouseCursor.ResizeHorizontal);
            }

            var current = Event.current;

            switch (current.GetTypeForControl(id))
            {
                case EventType.MouseDown when current.button == 0 && rect.Contains(current.mousePosition):
                {
                    GUIUtility.hotControl = id;
                    _dragged = PickHandle(rect, value, current.mousePosition.x);
                    value = Drag(rect, value, current.mousePosition.x);
                    GUI.changed = true;
                    current.Use();
                    break;
                }

                case EventType.MouseDrag when GUIUtility.hotControl == id:
                {
                    value = Drag(rect, value, current.mousePosition.x);
                    GUI.changed = true;
                    current.Use();
                    break;
                }

                case EventType.MouseUp when GUIUtility.hotControl == id:
                {
                    GUIUtility.hotControl = 0;
                    current.Use();
                    break;
                }
            }

            return value;
        }

        private static int PickHandle(Rect rect, Vector2 value, float mouseX)
        {
            var position = Mathf.InverseLerp(rect.xMin, rect.xMax, mouseX);

            // Coinciding borders are separated toward the click.
            if (Mathf.Approximately(value.x, value.y))
                return position < value.x ? 0 : 1;

            return Mathf.Abs(position - value.x) <= Mathf.Abs(position - value.y) ? 0 : 1;
        }

        private static Vector2 Drag(Rect rect, Vector2 value, float mouseX)
        {
            var position = Mathf.Round(Mathf.InverseLerp(rect.xMin, rect.xMax, mouseX) * 100f) / 100f;

            if (_dragged == 0)
                value.x = Mathf.Min(position, value.y);
            else
                value.y = Mathf.Max(position, value.x);

            return value;
        }

        // The sizes of the first two parts are edited, the third is the remainder.
        private static Vector2 DrawFields(Rect rect, Vector2 value, string[] names)
        {
            var labels = new[] { new GUIContent(names[0]), new GUIContent(names[1]) };
            var sizes = new[] { value.x, value.y - value.x };

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.MultiFloatField(rect, labels, sizes);
            EditorGUI.indentLevel = indent;

            var first = Mathf.Clamp01(sizes[0]);
            var second = Mathf.Clamp(sizes[1], 0f, 1f - first);

            return new Vector2(first, first + second);
        }

        private static void DrawSegmentText(Rect segment, string name, float size, float? total)
        {
            _segmentStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.black }
            };

            var amount = total.HasValue
                ? $"{size * total.Value:0.###}s"
                : $"{size * 100f:0}%";

            var full = new GUIContent($"{name} {amount}");
            var text = _segmentStyle.CalcSize(full).x <= segment.width ? full : new GUIContent(amount);

            if (_segmentStyle.CalcSize(text).x <= segment.width)
                GUI.Label(segment, text, _segmentStyle);
        }

        private static float? FindTotal(SerializedProperty property, SplitSliderAttribute split)
        {
            if (string.IsNullOrEmpty(split.TotalProperty))
                return null;

            var total = property.serializedObject.FindProperty(split.TotalProperty);

            if (total == null || total.propertyType != SerializedPropertyType.Float)
                return null;

            return total.floatValue;
        }

        private static Vector2 Normalize(Vector2 value)
        {
            var first = Mathf.Clamp01(value.x);
            return new Vector2(first, Mathf.Clamp(value.y, first, 1f));
        }

        private static void DrawFallback(Rect rect, SerializedProperty property, GUIContent label)
        {
            float helpBoxHeight = EditorGUIUtility.singleLineHeight * 2f;

            var helpBoxRect = new Rect(rect.x, rect.y, rect.width, helpBoxHeight);
            EditorGUI.HelpBox(helpBoxRect, $"{nameof(SplitSliderAttribute)} can be used only on Vector2 fields", MessageType.Warning);

            var propertyRect = new Rect(
                rect.x,
                rect.y + helpBoxHeight + EditorGUIUtility.standardVerticalSpacing,
                rect.width,
                EditorGUI.GetPropertyHeight(property, label));

            EditorGUI.PropertyField(propertyRect, property, label, true);
        }
    }
}
