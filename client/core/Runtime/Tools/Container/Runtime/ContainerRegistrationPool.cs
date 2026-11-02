using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    // The lists live from the first registration until the build and go back here: building runs only on the main
    // thread (ContainerThread.Assert), and array growth is paid once per process.
    [NoAutoStaticsCleanup]
    internal static class ContainerRegistrationPool
    {
        private static readonly Stack<List<ContainerEntry>> _entries = new Stack<List<ContainerEntry>>();
        private static readonly Stack<List<ContainerHole>> _holes = new Stack<List<ContainerHole>>();

        public static List<ContainerEntry> TakeEntries()
        {
            return _entries.Count > 0 ? _entries.Pop() : new List<ContainerEntry>();
        }

        public static List<ContainerHole> TakeHoles()
        {
            return _holes.Count > 0 ? _holes.Pop() : new List<ContainerHole>();
        }

        public static void Return(List<ContainerEntry> entries)
        {
            entries.Clear();
            _entries.Push(entries);
        }

        public static void Return(List<ContainerHole> holes)
        {
            holes.Clear();
            _holes.Push(holes);
        }
    }
}
