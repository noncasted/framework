using UnityEngine;

namespace ContentSmoke
{
    // The asset behind the Loadable of the root.
    public sealed class ContentSmokeTarget : ScriptableObject
    {
        [SerializeField] private string _text;

        public string Text => _text;
    }
}
