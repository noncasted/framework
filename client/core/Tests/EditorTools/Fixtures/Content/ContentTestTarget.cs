using UnityEngine;

namespace Internal.EditorTests.Content
{
    // A lazy target of the content tests: a direct reference to another asset, which the bundle of the target needs
    // (in the same bundle, or in another one it then depends on).
    public sealed class ContentTestTarget : ScriptableObject
    {
        [SerializeField] private Object _direct;

        public Object Direct => _direct;

        public void Init(Object direct)
        {
            _direct = direct;
        }
    }
}
