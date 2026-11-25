using Unity.Scripting.LifecycleManagement;

namespace Internal.Tests
{
    [NoAutoStaticsCleanup]
    public sealed class ScopeCodegenScoped
    {
        public static int Instances { get; private set; }

        public static void ResetInstances()
        {
            Instances = 0;
        }

        public ScopeCodegenScoped()
        {
            Instances++;
        }
    }
}
