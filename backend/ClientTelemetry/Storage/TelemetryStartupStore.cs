using Infrastructure.State;

namespace Game.ClientTelemetry;

// The stored side of the startup traces for the hosts: no grain is activated here.
internal sealed class TelemetryStartupStore
{
    public TelemetryStartupStore(ITelemetryStartupCollection startups, IStateStorage storage)
    {
        _startups = startups;
        _storage = storage;
    }

    private readonly ITelemetryStartupCollection _startups;
    private readonly IStateStorage _storage;

    public IReadOnlyList<TelemetryStartupInfo> List(int limit)
    {
        return _startups.Values
                        .Select(startup => startup.ToInfo())
                        .OrderByDescending(startup => startup.ReceivedAt)
                        .ThenBy(startup => startup.Id)
                        .Take(limit)
                        .ToArray();
    }

    // Null: no such record. As TelemetrySessionStore: a record this host has not heard about yet is looked for in its row.
    public async Task<TelemetryStartupState?> Find(Guid id)
    {
        if (_startups.TryGetValue(id, out var known))
            return known;

        var stored = await _storage.Read<TelemetryStartupState>(_storage.IdentityOf<TelemetryStartupState>(id));
        return stored.IsStored ? stored : null;
    }

    public async Task<IReadOnlyList<TelemetryStartupSpan>> ReadSpans(Guid id)
    {
        return (await _storage.Read<TelemetryStartupTraceState>(_storage.IdentityOf<TelemetryStartupTraceState>(id))).Spans;
    }
}
