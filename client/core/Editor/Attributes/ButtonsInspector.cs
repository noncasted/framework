using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    public abstract class ButtonsInspector : UnityEditor.Editor
    {
        // DeclaredOnly + manual walk up the base types: FlattenHierarchy returns only statics from the bases,
        // and buttons often live on private methods of an abstract base.
        private const BindingFlags MethodFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly ILog _log = Logs.Of<ButtonsInspector>();

        private readonly List<(MethodInfo Method, string Text)> _buttons = new();

        protected virtual void OnEnable()
        {
            _buttons.Clear();

            if (target == null)
                return;

            foreach (MethodInfo method in GetMethods(target.GetType()))
            {
                var button = method.GetCustomAttribute<ButtonAttribute>(true);

                if (button == null)
                    continue;

                if (method.GetParameters().Length > 0)
                {
                    _log.Warning("{Attribute} works only with parameterless methods: {Method}", nameof(ButtonAttribute), method.Name);
                    continue;
                }

                _buttons.Add((method, button.Text ?? ObjectNames.NicifyVariableName(method.Name)));
            }
        }

        private static IEnumerable<MethodInfo> GetMethods(Type type)
        {
            var visited = new HashSet<MethodInfo>();

            for (Type current = type; current != null; current = current.BaseType)
            {
                if (current == typeof(MonoBehaviour) || current == typeof(ScriptableObject) ||
                    current == typeof(UnityEngine.Object) || current == typeof(object))
                    break;

                foreach (MethodInfo method in current.GetMethods(MethodFlags))
                {
                    if (visited.Add(method.GetBaseDefinition()))
                        yield return method;
                }
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawButtons();
        }

        protected void DrawButtons()
        {
            if (_buttons.Count == 0)
                return;

            EditorGUILayout.Space();

            foreach ((MethodInfo method, string text) in _buttons)
            {
                if (GUILayout.Button(text))
                    Invoke(method, text);
            }
        }

        private void Invoke(MethodInfo method, string text)
        {
            foreach (UnityEngine.Object current in targets)
            {
                if (current == null)
                    continue;

                Undo.RecordObject(current, text);

                try
                {
                    method.Invoke(current, null);
                }
                catch (TargetInvocationException exception)
                {
                    Debug.LogException(exception.InnerException ?? exception, current);
                }

                EditorUtility.SetDirty(current);
            }
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true)]
    public sealed class MonoBehaviourButtonsInspector : ButtonsInspector
    {
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true)]
    public sealed class ScriptableObjectButtonsInspector : ButtonsInspector
    {
    }
}
