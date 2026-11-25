using Unity.Scripting.LifecycleManagement;

namespace Internal.Tests
{
    [NoAutoStaticsCleanup]
    public sealed class ScopeCodegenTransient
    {
        public static int Instances { get; private set; }

        public static void ResetInstances()
        {
            Instances = 0;
        }

        public ScopeCodegenTransient(ScopeCodegenSingleton singleton)
        {
            Instances++;
            Singleton = singleton;
        }

        public ScopeCodegenSingleton Singleton { get; }
    }
}
