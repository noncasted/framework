using UnityEngine;

namespace ContentSmoke
{
    // The lazy target of the root.
    public sealed class ContentSmokeTarget : ScriptableObject
    {
        [SerializeField] private string _text;

        public string Text => _text;
    }
}
