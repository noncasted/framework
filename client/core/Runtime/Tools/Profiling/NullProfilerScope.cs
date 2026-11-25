using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public class NullProfilerScope : IProfilerScope
    {
        public static readonly NullProfilerScope Instance = new();

        private static readonly IProfilerScope[] _children = new IProfilerScope[0];

        public string Name => string.Empty;
        public bool IsRunning => false;
        public double StartMs => 0d;
        public double DurationMs => 0d;
        public int Frames => 0;
        public IReadOnlyList<IProfilerScope> Children => _children;

        public IProfilerScope Child(string name) => Instance;

        public void SetName(string name)
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}
