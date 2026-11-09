using Unity.Loading;
using UnityEngine;

namespace Internal.EditorTests.Content
{
    // A lazy reference in an object of a scene of the content build tests, left empty.
    public sealed class ContentTestBehaviour : MonoBehaviour
    {
        [SerializeField] private Loadable<ContentTestTarget> _target;
    }
}
