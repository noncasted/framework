using UnityEditor;
using UnityEngine;

namespace Internal
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true)]
    public sealed class ScriptableObjectButtonsInspector : ButtonsInspector
    {
    }
}
