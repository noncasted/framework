using Infrastructure;
using Infrastructure.State;

namespace Game.ClientTelemetry;

// The stored side of a session for the hosts: no grain is activated here.
internal sealed class TelemetrySessionStore
{
    public TelemetrySessionStore(ITelemetrySessionCollection sessions, IStateStorage storage)
    {
        _sessions = sessions;
        _storage = storage;
    }

    private readonly ITelemetrySessionCollection _sessions;
    private readonly IStateStorage _storage;

    // Null: no such session. The collection answers first; a session it has not heard about yet is looked for in its row, which
    // is written before the start is answered. So the database is asked only for garbage and in this short moment.
    public async Task<TelemetrySessionState?> Find(Guid id)
    {
        if (_sessions.TryGetValue(id, out var known))
            return known;

        var stored = await _storage.Read<TelemetrySessionState>(_storage.IdentityOf<TelemetrySessionState>(id));
        return stored.IsStarted ? stored : null;
    }

    public Task<TelemetrySessionLogState> ReadLog(Guid id)
    {
        return _storage.Read<TelemetrySessionLogState>(_storage.IdentityOf<TelemetrySessionLogState>(id));
    }
}
