using Shared;

namespace Global.MatchMetrics
{
    public class MatchSeriesRecord
    {
        public MatchSeriesRecord(int slot, string name, SharedMatchMetrics.MatchSeriesKind kind, MatchTags tags)
        {
            Slot = slot;
            Name = name;
            Kind = kind;
            Tags = tags;
        }

        public int Slot { get; }
        public string Name { get; }
        public SharedMatchMetrics.MatchSeriesKind Kind { get; }
        public MatchTags Tags { get; }
    }
}
