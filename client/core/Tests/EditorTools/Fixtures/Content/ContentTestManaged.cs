using UnityEngine;

namespace Internal.EditorTests.Content
{
    // An asset of the content tests with a managed reference: the type of its part is named only by the data of a bundle.
    public sealed class ContentTestManaged : ScriptableObject
    {
        [SerializeReference] private object _part;

        public object Part => _part;

        public void Init(object part)
        {
            _part = part;
        }
    }
}
