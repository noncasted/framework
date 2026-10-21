using UnityEngine;

namespace Internal
{
    // The normal start of the game. While it is in the loaded scenes, mocks do not run.
    [DisallowMultipleComponent]
    public abstract class StartupEntry : MonoBehaviour
    {
    }
}
