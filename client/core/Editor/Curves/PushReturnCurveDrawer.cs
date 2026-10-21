using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // Under the preview lies a single CurveField for the whole curve: a click opens the editor with push and return
    // together. The preview is drawn on top: push and return in their own colors, with the PushEnd border between them.
    [NoAutoStaticsCleanup]
    [CustomPropertyDrawer(typeof(PushReturnCurve))]
    [CustomPropertyDrawer(typeof(PushReturnRangeAttribute))]
    public sealed class PushReturnCurveDrawer : PropertyDrawer
    {
        private const float PreviewLines = 3f;
        private const int Samples = 48;
        private const float LineWidth = 2f;
        private const float DividerWidth = 2f;

        private static readonly Color BackgroundColor = new(0.17f, 0.17f, 0.17f);
        private static readonly Color ZeroColor = new(1f, 1f, 1f, 0.15f);
        private static readonly Color PushColor = new(0.15f, 0.7f, 0.75f);
        private static readonly Color ReturnColor = new(0.42f, 0.35f, 0.9f);
        private static readonly Color DividerColor = new(0.9f, 0.3f, 0.3f);

        private static readonly Vector3[] Points = new Vector3[Samples + 1];

        private static GUIStyle _captionStyle;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (FindCurve(property) == null)
                return EditorGUI.GetPropertyHeight(property, label, true);

            return EditorGUIUtility.singleLineHeight * PreviewLines;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            var curve = FindCurve(property);

            if (curve == null)
            {
                EditorGUI.PropertyField(rect, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(rect, label, property);

            var (min, max) = GetRange();
            var preview = EditorGUI.PrefixLabel(rect, label);

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var ranges = new Rect(0f, min, PushReturnCurve.ReturnEnd, max - min);
            EditorGUI.CurveField(preview, curve, PushColor, ranges, GUIContent.none);

            EditorGUI.indentLevel = indent;

            if (Event.current.type == EventType.Repaint && curve.hasMultipleDifferentValues == false)
                DrawPreview(preview, curve.animationCurveValue);

            EditorGUI.EndProperty();
        }

        private (float, float) GetRange()
        {
            return attribute is PushReturnRangeAttribute range ? (range.Min, range.Max) : (0f, 1f);
        }

        // Vertically the preview fits the curve values together with zero: otherwise a small peak in a wide
        // range would be impossible to see.
        private static void DrawPreview(Rect preview, AnimationCurve curve)
        {
            var low = 0f;
            var high = 0f;

            for (var i = 0; i <= Samples * 2; i++)
            {
                var value = curve.Evaluate(PushReturnCurve.ReturnEnd * i / (Samples * 2));
                low = Mathf.Min(low, value);
                high = Mathf.Max(high, value);
            }

            if (Mathf.Approximately(low, high))
                high = low + 1f;

            // Padding so a line a couple of pixels thick is not clipped by the edge.
            var area = new Rect(preview.x, preview.y + LineWidth, preview.width, preview.height - LineWidth * 2f);
            var dividerX = preview.x + preview.width * PushReturnCurve.PushEnd / PushReturnCurve.ReturnEnd;

            var pushRect = new Rect(preview.x, preview.y, dividerX - preview.x, preview.height);
            var returnRect = new Rect(dividerX, preview.y, preview.xMax - dividerX, preview.height);

            EditorGUI.DrawRect(preview, BackgroundColor);
            EditorGUI.DrawRect(new Rect(preview.x, ToY(area, 0f, low, high), preview.width, 1f), ZeroColor);

            DrawSegment(curve, pushRect, area, 0f, PushReturnCurve.PushEnd, low, high, PushColor);
            DrawSegment(curve, returnRect, area, PushReturnCurve.PushEnd, PushReturnCurve.ReturnEnd, low, high, ReturnColor);

            EditorGUI.DrawRect(new Rect(dividerX - DividerWidth * 0.5f, preview.y, DividerWidth, preview.height), DividerColor);

            _captionStyle ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };

            GUI.Label(pushRect, "Push", _captionStyle);
            GUI.Label(returnRect, "Return", _captionStyle);
        }

        private static void DrawSegment(
            AnimationCurve curve,
            Rect segment,
            Rect area,
            float from,
            float to,
            float low,
            float high,
            Color color)
        {
            for (var i = 0; i <= Samples; i++)
            {
                var t = (float)i / Samples;
                var value = curve.Evaluate(Mathf.Lerp(from, to, t));
                Points[i] = new Vector3(segment.x + segment.width * t, ToY(area, value, low, high), 0f);
            }

            Handles.color = color;
            Handles.DrawAAPolyLine(LineWidth, Points);
        }

        private static float ToY(Rect area, float value, float low, float high)
        {
            return area.yMax - Mathf.InverseLerp(low, high, value) * area.height;
        }

        private static SerializedProperty FindCurve(SerializedProperty property)
        {
            var curve = property.FindPropertyRelative("_curve");

            if (curve == null || curve.propertyType != SerializedPropertyType.AnimationCurve)
                return null;

            return curve;
        }
    }
}
