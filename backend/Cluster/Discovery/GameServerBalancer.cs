using Cluster.Deploy;
using Cluster.Monitoring;
using Common.Extensions;

namespace Cluster.Discovery;

public interface IGameServerBalancer
{
    // The game server for a new session: the one with the fewest live sessions; a tie - a random one of them.
    GameServerOverview Pick();
}

// Sessions are counted from LiveSessionsData (every game gateway writes its slice under its service id when a
// session is added or removed). A server without a slice yet has no sessions. A burst of requests faster than the
// slice update goes to one server - the count catches up with the next update.
public class GameServerBalancer : IGameServerBalancer
{
    public GameServerBalancer(IServiceDiscovery discovery, IDeploymentState<LiveSessionsData> liveSessions)
    {
        _discovery = discovery;
        _liveSessions = liveSessions;
    }

    private readonly IServiceDiscovery _discovery;
    private readonly IDeploymentState<LiveSessionsData> _liveSessions;

    public GameServerOverview Pick()
    {
        var slices = _liveSessions.Value.Slices;
        var candidates = new List<GameServerOverview>();
        var fewest = int.MaxValue;

        foreach (var server in _discovery.Entries.Values.OfType<GameServerOverview>())
        {
            var count = slices.TryGetValue(server.Id, out var sessions) == true ? sessions.Count : 0;

            if (count > fewest)
                continue;

            if (count < fewest)
            {
                fewest = count;
                candidates.Clear();
            }

            candidates.Add(server);
        }

        if (candidates.Count == 0)
            throw new InvalidOperationException("No game servers available");

        return candidates.Random();
    }
}
