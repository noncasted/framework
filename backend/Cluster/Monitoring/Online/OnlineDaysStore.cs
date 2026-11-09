using Infrastructure;
using Infrastructure.State;

namespace Cluster.Monitoring.Online;

public interface IOnlineDaysStore
{
    // The stored days from the first to the last day number, both included; a day without a row is absent.
    Task<IReadOnlyList<OnlineDayState>> Read(int firstDay, int lastDay);

    Task Write(IReadOnlyList<OnlineDayState> days);
}

// The rows of OnlineDayState, one per UTC day, keyed by its day number.
public class OnlineDaysStore : IOnlineDaysStore
{
    public OnlineDaysStore(IOrleans orleans)
    {
        _storage = orleans.StateStorage;
    }

    private readonly IStateStorage _storage;

    public async Task<IReadOnlyList<OnlineDayState>> Read(int firstDay, int lastDay)
    {
        var identities = Enumerable.Range(firstDay, lastDay - firstDay + 1).Select(Identity).ToList();
        var stored = await _storage.ReadBatch<long, OnlineDayState>(identities);
        return stored.Values.OrderBy(day => day.Day).ToList();
    }

    public Task Write(IReadOnlyList<OnlineDayState> days)
    {
        if (days.Count == 0)
            return Task.CompletedTask;

        return _storage.Write(new StateWriteRequest
        {
            Records = days.ToDictionary(day => Identity(day.Day), IStateValue (day) => day)
        });
    }

    private StateIdentity Identity(int day)
    {
        var info = _storage.Registry.Get<OnlineDayState>();

        return new StateIdentity
        {
            Key = (long)day,
            Type = info.Name,
            TableName = info.TableName,
            Extension = null
        };
    }
}
