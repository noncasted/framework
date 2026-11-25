using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Scripting.LifecycleManagement;

namespace Internal.Tests
{
    // Records what the scopes of the loader tests went through, in order.
    [NoAutoStaticsCleanup]
    public static class ScopeLoaderProbe
    {
        public static readonly List<string> Log = new List<string>();
        public static readonly Dictionary<string, IReadOnlyLifetime> Lifetimes = new Dictionary<string, IReadOnlyLifetime>();
        public static IContainer Container { get; private set; }
        public static UniTaskCompletionSource Gate { get; private set; }

        public static void Reset()
        {
            Log.Clear();
            Lifetimes.Clear();
            Container = null;
            Gate = null;
        }

        public static void SetContainer(IContainer container)
        {
            Container = container;
        }

        public static void SetGate(UniTaskCompletionSource gate)
        {
            Gate = gate;
        }

        public static int Count(string entry)
        {
            var count = 0;

            for (var i = 0; i < Log.Count; i++)
            {
                if (Log[i] == entry)
                    count++;
            }

            return count;
        }

        // The asset groups of a scope are released the same way: an after-dispose callback of its event loop.
        public static void Track(string name, IEventLoop events, IReadOnlyLifetime lifetime)
        {
            Lifetimes[name] = lifetime;
            events.AddAfterDispose(() =>
            {
                Log.Add(name + ".afterDispose");
                return UniTask.CompletedTask;
            });
        }
    }
}
