using Game.ClientTelemetry;
using System.Collections.Concurrent;
using Infrastructure.State;
using Internal;
using Npgsql;

namespace Framework.Tests.ClientTelemetry;

// Only the calls with the states of the telemetry count, by session: the other states of the cluster and the other sessions,
// which a shared clock may close at any moment, live their own life. A batch is one write.
public sealed class CountingStateStorage : IStateStorage
{
    public CountingStateStorage(IStateStorage inner)
    {
        _inner = inner;
    }

    private readonly IStateStorage _inner;
    private readonly ConcurrentDictionary<Guid, int> _reads = new();
    private readonly ConcurrentDictionary<Guid, int> _writes = new();
    private int _failures;
    private int _readsOfTheSwitch;
    private volatile bool _failWrites;

    public int ReadsOf(Guid session) => _reads.GetValueOrDefault(session);
    public int WritesOf(Guid session) => _writes.GetValueOrDefault(session);
    public int Failures => Volatile.Read(ref _failures);
    public int ReadsOfTheSwitch => Volatile.Read(ref _readsOfTheSwitch);
    public IGrainStatesRegistry Registry => _inner.Registry;

    public void ResetFailures() => Interlocked.Exchange(ref _failures, 0);

    public void FailWrites(bool fail) => _failWrites = fail;

    public Task<T> Read<T>(StateIdentity identity) where T : IStateValue, new()
    {
        if (IsTheSwitch(identity))
            Interlocked.Increment(ref _readsOfTheSwitch);
        else if (IsTelemetry(identity))
            Count(_reads, identity);

        return _inner.Read<T>(identity);
    }

    public Task Write(StateWriteRequest request)
    {
        var telemetry = request.Records.Keys.FirstOrDefault(IsTelemetry);

        if (telemetry == null)
            return _inner.Write(request);

        if (_failWrites)
        {
            Interlocked.Increment(ref _failures);
            throw new InvalidOperationException("The write of the test failed");
        }

        if (!IsTheSwitch(telemetry))
            Count(_writes, telemetry);

        return _inner.Write(request);
    }

    public Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(IReadOnlyList<StateIdentity> identities)
        where TKey : notnull
        where TValue : class, IStateValue, new()
    {
        return _inner.ReadBatch<TKey, TValue>(identities);
    }

    public IAsyncEnumerable<(TKey, TValue)> ReadAll<TKey, TValue>(IReadOnlyLifetime lifetime)
        where TKey : notnull
        where TValue : class, IStateValue, new()
    {
        return _inner.ReadAll<TKey, TValue>(lifetime);
    }

    public IAsyncEnumerable<TKey> ReadAllKeys<TKey>(GrainStateInfo stateInfo, IReadOnlyLifetime lifetime) where TKey : notnull
    {
        return _inner.ReadAllKeys<TKey>(stateInfo, lifetime);
    }

    public IReadOnlyList<NpgsqlBatchCommand> BuildWriteCommands(IReadOnlyDictionary<StateIdentity, IStateValue> records)
    {
        return _inner.BuildWriteCommands(records);
    }

    public Task Delete(StateDeleteRequest request) => _inner.Delete(request);

    public Task<string> ReadRawJson(StateIdentity identity) => _inner.ReadRawJson(identity);

    private static void Count(ConcurrentDictionary<Guid, int> counts, StateIdentity identity)
    {
        counts.AddOrUpdate((Guid)identity.Key, 1, (_, count) => count + 1);
    }

    private static bool IsTelemetry(StateIdentity identity) => identity.Type.StartsWith("client_telemetry_");
    private static bool IsTheSwitch(StateIdentity identity) => identity.Type == ClientTelemetrySettingsState.StateName;
}
