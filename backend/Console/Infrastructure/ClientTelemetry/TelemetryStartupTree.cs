using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

// The tree of the window of a startup trace over its spans, like the window of the editor: it opens with the roots expanded, then
// the long stages are opened by hand. The expanded spans belong to the page: a new snapshot of the trace keeps what was opened.
public sealed class TelemetryStartupTree
{
    public TelemetryStartupTree(IReadOnlyList<TelemetryStartupSpanFacts> spans, double traceDurationMs)
    {
        Spans = spans;
        _children = spans.ToLookup(span => span.ParentIndex);

        // A span of a snapshot may end after the duration the client told: the scale holds everything.
        ScaleMs = Math.Max(1, spans.Select(span => span.StartMs + span.DurationMs).Append(traceDurationMs).Max());
    }

    private const int _noParent = -1;

    private readonly ILookup<int, TelemetryStartupSpanFacts> _children;

    public IReadOnlyList<TelemetryStartupSpanFacts> Spans { get; }
    public double ScaleMs { get; }

    // No platform but the editor measures allocations: the column stands only when there is something to show.
    public bool HasAllocations => Spans.Any(span => span.AllocBytes > 0);

    public IEnumerable<int> Roots => _children[_noParent].Select(span => span.Index);
    public IEnumerable<int> All => Spans.Select(span => span.Index);

    public bool HasChildren(int index)
    {
        return _children.Contains(index);
    }

    // Children right under the parent, by their start. A stack: a depth is limited only by the number of spans.
    public IReadOnlyList<TelemetryStartupSpanFacts> Visible(IReadOnlySet<int> expanded)
    {
        var rows = new List<TelemetryStartupSpanFacts>();
        var stack = new Stack<TelemetryStartupSpanFacts>(Ordered(_noParent));

        while (stack.TryPop(out var span))
        {
            rows.Add(span);

            if (!expanded.Contains(span.Index))
                continue;

            foreach (var child in Ordered(span.Index))
                stack.Push(child);
        }

        return rows;
    }

    // The own time, not the duration: a parent holds the time of everything under it. Waiting for a player shows up here too.
    public IReadOnlyList<TelemetryStartupSpanFacts> LongestOwn(int count)
    {
        return Spans.OrderByDescending(span => span.OwnMs).ThenBy(span => span.Index).Take(count).ToArray();
    }

    // Reversed for the stack.
    private IEnumerable<TelemetryStartupSpanFacts> Ordered(int parent)
    {
        return _children[parent].OrderByDescending(span => span.StartMs).ThenByDescending(span => span.Index);
    }
}
