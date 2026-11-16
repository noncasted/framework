using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Quitting the application (or Play mode) has already begun: Unity unloads the scenes and the loaded AssetBundles
    /// itself, so the content releases nothing by then. The
    /// root scope sets the flag before it terminates the lifetimes (see <see cref="RootScopeExtensions"/>).
    /// </summary>
    public static class ApplicationQuit
    {
        public static bool IsQuitting { get; private set; }

        public static void Mark()
        {
            IsQuitting = true;
        }

        // The domain is not reloaded between Play mode runs: the static outlives the exit.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            IsQuitting = false;
        }
    }
}
