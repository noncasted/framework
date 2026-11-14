using Internal;

namespace Console;

public interface ISideEffectsAggregator
{
    IViewableProperty<SideEffectsAggregate> Aggregate { get; }
}
