using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // A scene opened directly in the editor starts through its mock: the mock loads the scope chain the scene needs and
    // runs it (MockSwitcher calls Process on entering Play Mode).
    [DisallowMultipleComponent]
    public abstract class MockEntry : MonoBehaviour
    {
        public abstract UniTaskVoid Process();
    }
}
