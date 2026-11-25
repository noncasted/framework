using UnityEngine;

namespace Internal
{
    // A listener of a lifetime or a handler of an event source that threw is logged, the others still run
    // (ReactiveExceptions of Framework.Common). Installed before the first scene and in the editor on load.
    public static class ReactiveLogging
    {
        private static readonly ILog _log = Logs.Of(typeof(ReactiveExceptions));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        private static void Install()
        {
            ReactiveExceptions.SetHandler(exception => _log.Exception(exception));
        }
    }
}
