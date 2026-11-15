using UnityEngine;

namespace Internal.EditorTests.Content
{
    // A lazy reference and a direct one in an object of a scene of the content build tests.
    public sealed class ContentTestBehaviour : MonoBehaviour
    {
        [SerializeField] private ContentReference<ContentTestTarget> _target;
        [SerializeField] private Object _direct;
    }
}
