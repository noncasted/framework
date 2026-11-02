using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Shared;

namespace Cluster.Monitoring;

// Sessions of every game gateway, one slice per gateway service id (written via IServiceSlicedState).
// Registered as a deploy-scoped state so that IDeployCleanup removes the rows of old deploys. The rows hold an
// AddressableStateValue (DeploymentState), the state name is the one DeploymentState.CreateDefaultStateIdentity gives.
[GenerateSerializer]
[GrainState(Table = "cluster", State = "deployment_state_LiveSessionsData", Lookup = "LiveSessions", Key = GrainKeyType.Guid)]
public class LiveSessionsData : IServiceSlices<List<LiveSessionEntry>>, IDirectStateValue, IDeployScopedState
{
    [Id(1)] public Dictionary<Guid, List<LiveSessionEntry>> Slices { get; init; } = new();

    public int Version => 0;
}

[GenerateSerializer]
public class LiveSessionEntry
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public List<Guid> UserIds { get; init; } = [];
    [Id(2)] public DateTime CreatedAt { get; init; }
}
