using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Cluster.Lobbies;

[GenerateSerializer]
[GrainState(Table = "cluster", State = "lobby_registry", Lookup = "LobbyRegistry", Key = GrainKeyType.Guid)]
public class LobbyRegistryState : IDirectStateValue, IDeployScopedState
{
    // Code -> the lobby session that holds it (and the match of the lobby, once it has started).
    [Id(0)] [JsonProperty(nameof(Entries))] private Dictionary<string, LobbyRegistryEntry> _entries = new();

    // Version of the last pushed OpenLobbies.
    [Id(1)] [JsonProperty] public long ListingVersion { get; private set; }

    [JsonIgnore] public IReadOnlyDictionary<string, LobbyRegistryEntry> Entries => _entries;

    public int Version => 0;

    public void Add(string code, LobbyRegistryEntry entry)
    {
        _entries.Add(code, entry);
    }

    public void Remove(string code)
    {
        _entries.Remove(code);
    }

    public void AdvanceListingVersion()
    {
        ListingVersion++;
    }
}
