using Cluster.Lobbies;
using Common.Extensions;
using Game.Session;
using Microsoft.Extensions.Logging;

namespace Game.Lobbies;

// Who holds the code of a lobby of this game server and when the code goes back to the registry.
// - A lobby that has not started holds its code itself: the code is released when the lobby is closed.
// - At zero of the start countdown the code passes to the match the lobby has created: the registry entry is
//   marked as started (a join by the code leads to the lobby of the late players of the match) and the code is
//   released when the MATCH is closed - its last player left, the game ended, nobody entered in time. The
//   lobby session, closed about a second after zero, does not touch the code any more.
// The registry calls of one code (mark, listing reports, release) go one after another, in the order they are
// made here: a report never overtakes the mark, the release is the last call. Of the reports queued behind a
// slow call only the latest one is sent.
// A game server that died keeps its codes in the registry until the next deploy, started or not.
public interface ILobbyCodeLeases
{
    void Hold(string code, ISession lobby);

    // Called from the queue of the lobby session, while the lobby is alive.
    void PassToMatch(Guid lobbySessionId, ISession match);

    // The match of the lobby can take a new player now (the last listing its match has reported). False for a
    // lobby that has not started or whose code is released.
    bool IsMatchJoinable(Guid lobbySessionId);
}

public class LobbyCodeLeases : ILobbyCodeLeases, ILobbyListings
{
    public LobbyCodeLeases(ILobbyCodes codes, ILogger<LobbyCodeLeases> logger)
    {
        _codes = codes;
        _logger = logger;
    }

    private readonly ILobbyCodes _codes;
    private readonly ILogger<LobbyCodeLeases> _logger;

    // Lobby session -> its lease; an entry lives until the code is released (with the lobby or with its match).
    private readonly Dictionary<Guid, Lease> _leases = new();
    private readonly Lock _lock = new();

    public void Hold(string code, ISession lobby)
    {
        var lobbySessionId = lobby.Id;

        lock (_lock)
            _leases.Add(lobbySessionId, new Lease(code, lobbySessionId));

        // The lifetime of a session is terminated exactly once, when it is closed.
        lobby.Lifetime.Listen(() => OnLobbyClosed(lobbySessionId));
    }

    public void PassToMatch(Guid lobbySessionId, ISession match)
    {
        Lease? lease;

        lock (_lock)
        {
            if (_leases.TryGetValue(lobbySessionId, out lease) == false)
                throw new InvalidOperationException($"Lobby session {lobbySessionId} holds no code");

            if (lease.IsPassed == true)
                throw new InvalidOperationException($"The code of lobby session {lobbySessionId} is already passed");

            lease.IsPassed = true;
            Enqueue(lease, () => _codes.MarkStarted(lease.Code, lobbySessionId, match.Id));

            // The lobby is not shown any more; the match shows itself once its players are in.
            ReportLocked(lease, new LobbyListing
            {
                IsOpen = lease.Listing?.IsOpen ?? false,
                MaxPlayers = lease.Listing?.MaxPlayers ?? 0,
                IsRunning = true,
                IsJoinable = true
            });
        }

        _logger.LogInformation("[Lobby] Code {Code} of lobby session {LobbySessionID} is held for match {SessionID}",
            lease.Code,
            lobbySessionId,
            match.Id);

        match.Lifetime.Listen(() => {
            lock (_lock)
            {
                // The lobby session is usually closed long before; if not, it forgets the lease when it is.
                if (lease.IsLobbyClosed == true)
                    _leases.Remove(lobbySessionId);

                Release(lease);
            }

            _logger.LogInformation("[Lobby] Code {Code} is released: match {SessionID} is closed", lease.Code, match.Id);
        });
    }

    public bool IsMatchJoinable(Guid lobbySessionId)
    {
        lock (_lock)
        {
            if (_leases.TryGetValue(lobbySessionId, out var lease) == false ||
                lease.IsPassed == false ||
                lease.IsReleased == true)
                return false;

            return lease.Listing!.IsJoinable == true;
        }
    }

    public void Report(Guid lobbySessionId, LobbyListing listing)
    {
        lock (_lock)
        {
            // The code is released (a player left the match at the moment it was closed): nothing to show.
            if (_leases.TryGetValue(lobbySessionId, out var lease) == false || lease.IsReleased == true)
                return;

            ReportLocked(lease, listing);
        }
    }

    // Under the lock.
    private void ReportLocked(Lease lease, LobbyListing listing)
    {
        lease.Listing = listing;

        if (lease.IsReportQueued == true)
            return;

        lease.IsReportQueued = true;

        Enqueue(lease, () => {
            LobbyListing latest;

            lock (_lock)
            {
                lease.IsReportQueued = false;
                latest = lease.Listing!;
            }

            return _codes.UpdateListing(lease.Code, lease.LobbySessionId, latest);
        });
    }

    private void OnLobbyClosed(Guid lobbySessionId)
    {
        lock (_lock)
        {
            if (_leases.TryGetValue(lobbySessionId, out var lease) == false)
                throw new InvalidOperationException($"Closed lobby session {lobbySessionId} holds no code");

            lease.IsLobbyClosed = true;

            // The match of the lobby holds the code: the lease lives until the match is closed.
            if (lease.IsPassed == true)
            {
                if (lease.IsReleased == true)
                    _leases.Remove(lobbySessionId);

                return;
            }

            _leases.Remove(lobbySessionId);
            Release(lease);
        }
    }

    // Under the lock.
    private void Release(Lease lease)
    {
        lease.IsReleased = true;
        Enqueue(lease, () => _codes.Release(lease.Code, lease.LobbySessionId));
    }

    // Under the lock. A failed call is logged and does not stop the calls after it.
    private void Enqueue(Lease lease, Func<Task> call)
    {
        lease.Tail = Run(lease.Tail);
        lease.Tail.NoAwait();

        async Task Run(Task previous)
        {
            await previous;

            try
            {
                await call();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Lobby] Registry call for code {Code} failed", lease.Code);
            }
        }
    }

    private class Lease
    {
        public Lease(string code, Guid lobbySessionId)
        {
            Code = code;
            LobbySessionId = lobbySessionId;
        }

        public readonly string Code;
        public readonly Guid LobbySessionId;

        public bool IsPassed;
        public bool IsLobbyClosed;
        public bool IsReleased;

        // The last reported listing (of the lobby, then of its match); null until the lobby reports itself.
        public LobbyListing? Listing;
        public bool IsReportQueued;

        // The last registry call of the code.
        public Task Tail = Task.CompletedTask;
    }
}
