using UnityEditor;

namespace Internal
{
    /// <summary>
    /// Marks the first run after a recompilation. The domain is not reloaded on entering
    /// Play Mode, so the static constructor here fires exactly when
    /// the assemblies were rebuilt and the editor dropped its asset cache - such a run
    /// is cold and its numbers must not be compared with a warm one.
    /// </summary>
    [InitializeOnLoad]
    internal static class ProfilerDomainWatcher
    {
        static ProfilerDomainWatcher()
        {
            ProfilerTraceStorage.ColdDomain = true;
        }
    }
}