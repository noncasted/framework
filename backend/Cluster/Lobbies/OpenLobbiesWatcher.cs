using Common.Extensions;
using Cluster.Deploy;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging;

namespace Cluster.Lobbies;

// The open lobbies of the current deploy on this host: the snapshot of the registry, then every change it pushes on
// OpenLobbiesChannelId. A snapshot older than the one held (a late push, the answer to the first read) is dropped by
// its version.
public interface IOpenLobbiesWatcher
{
    IViewableProperty<OpenLobbies> Lobbies { get; }
}

public class OpenLobbiesWatcher : IOpenLobbiesWatcher, IDeployAware
{
    public OpenLobbiesWatcher(IOrleans orleans, IMessaging messaging, ILogger<OpenLobbiesWatcher> logger)
    {
        _orleans = orleans;
        _messaging = messaging;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly ILogger<OpenLobbiesWatcher> _logger;
    private readonly ViewableProperty<OpenLobbies> _lobbies = new(Empty());
    private readonly Lock _lock = new();

    public IViewableProperty<OpenLobbies> Lobbies => _lobbies;

    public async Task OnDeployChanged(Guid newDeployId, IReadOnlyLifetime deployLifetime)
    {
        lock (_lock)
            _lobbies.Set(Empty());

        // Listened before the read: a change made in between comes by the channel, the read never hides it. A gap of
        // the channel (missed pushes) reads the registry again; the version keeps the newer snapshot.
        await _messaging.ListenChannel<OpenLobbies>(deployLifetime, new OpenLobbiesChannelId(newDeployId), Apply,
            () => Reload(newDeployId).NoAwait());

        var current = await Reload(newDeployId);

        _logger.LogInformation("[Lobby] Open lobbies of deploy {DeployId}: {Count}", newDeployId, current.Entries.Count);
    }

    private async Task<OpenLobbies> Reload(Guid deployId)
    {
        var current = await _orleans.GetGrain<ILobbyRegistry>(deployId).GetOpenLobbies();
        Apply(current);
        return current;
    }

    private void Apply(OpenLobbies lobbies)
    {
        lock (_lock)
        {
            if (lobbies.Version <= _lobbies.Value.Version)
                return;

            _lobbies.Set(lobbies);
        }
    }

    // Older than any snapshot of the registry (its versions start from 0).
    private static OpenLobbies Empty()
    {
        return new OpenLobbies
        {
            Version = -1
        };
    }
}
