using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Cluster.Deploy;
using Cluster.Monitoring;
using Common.Extensions;
using Game.Session;
using Internal;

namespace Game.Hosting;

// The live sessions of this game server. Read by the search and by the handlers of the meta requests, modified by the
// factory of the game and by the session queues; every change goes to the live data of the cluster (LiveSessionsData).
public interface ISessionsCollection
{
    IReadOnlyDictionary<Guid, ISession> Entries { get; }

    void Add(ISession session);

    // The moment the session was added; false for a session that is not in the collection (already closed).
    bool TryGetCreatedAt(Guid id, out DateTime createdAt);

    bool TryGet(Guid id, [MaybeNullWhen(false)] out ISession session);
}

public class SessionsCollection : ISessionsCollection
{
    public SessionsCollection(IServiceSlicedState<LiveSessionsData, List<LiveSessionEntry>> liveData, TimeProvider time)
    {
        _liveData = liveData;
        _time = time;
    }

    private readonly IServiceSlicedState<LiveSessionsData, List<LiveSessionEntry>> _liveData;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, ISession> _entries = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _createdAt = new();

    // Snapshots of session users, replaced from the queue of the owning session: live data is pushed
    // from any thread and must not enumerate the user list of a foreign session.
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<Guid>> _userIds = new();

    // The queues of many sessions push at once: a snapshot is built and handed over under one lock, so the slices
    // reach SetSlice in the order of their state (the latest call wins there) and an older one never comes last.
    private readonly Lock _pushLock = new();

    public IReadOnlyDictionary<Guid, ISession> Entries => _entries;

    public void Add(ISession session)
    {
        if (_entries.TryAdd(session.Id, session) == false)
            throw new InvalidOperationException($"Session {session.Id} is already added");

        _createdAt[session.Id] = _time.GetUtcNow().UtcDateTime;
        _userIds[session.Id] = [];

        // The list lifetime of a user ends after the user is removed from the list.
        session.Users.View(session.Lifetime, (userLifetime, _) => {
            OnUsersChanged();
            userLifetime.Listen(OnUsersChanged);
        });

        session.Lifetime.Listen(() => {
            _entries.TryRemove(session.Id, out _);
            _createdAt.TryRemove(session.Id, out _);
            _userIds.TryRemove(session.Id, out _);
            PushLiveData();
        });

        PushLiveData();

        void OnUsersChanged()
        {
            if (session.Lifetime.IsTerminated == true)
                return;

            _userIds[session.Id] = session.Users.Select(user => user.BackendId).ToList();
            PushLiveData();
        }
    }

    public bool TryGetCreatedAt(Guid id, out DateTime createdAt)
    {
        return _createdAt.TryGetValue(id, out createdAt);
    }

    public bool TryGet(Guid id, [MaybeNullWhen(false)] out ISession session)
    {
        return _entries.TryGetValue(id, out session);
    }

    private void PushLiveData()
    {
        lock (_pushLock)
        {
            _liveData.SetSlice(_entries.Values.Select(s => new LiveSessionEntry
            {
                Id = s.Id,
                UserIds = _userIds.GetValueOrDefault(s.Id, []).ToList(),
                CreatedAt = _createdAt.GetValueOrDefault(s.Id, _time.GetUtcNow().UtcDateTime)
            }).ToList()).NoAwait();
        }
    }
}
