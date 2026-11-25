using UnityEditor;
using UnityEngine;

namespace Internal
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true)]
    public sealed class MonoBehaviourButtonsInspector : ButtonsInspector
    {
    }
}
