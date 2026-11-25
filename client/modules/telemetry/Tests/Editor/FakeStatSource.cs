using System.Collections.Generic;
using System.Linq;
using Internal;

namespace Global.Telemetry.Tests
{
    public class FakeStatSource : ITelemetryStatSource
    {
        public FakeStatSource(IReadOnlyList<string> available)
        {
            _available = available;
        }

        private readonly IReadOnlyList<string> _available;
        private readonly List<FakeStat> _stats = new List<FakeStat>();

        public List<string> Opened { get; } = new List<string>();
        public List<string> Categories { get; } = new List<string>();

        public bool TryOpen(IReadOnlyLifetime lifetime, string category, string name, out ITelemetryStat stat)
        {
            Categories.Add(category);
            stat = null;

            if (_available.Contains(name) == false)
                return false;

            var opened = new FakeStat();
            _stats.Add(opened);
            Opened.Add(name);
            stat = opened;
            return true;
        }

        public void SetAll(long value)
        {
            foreach (var stat in _stats)
                stat.Set(value);
        }
    }
}
