using Cluster.Deploy;
using Common;
using Infrastructure.State;
using Shared;

namespace Cluster.Monitoring;

// Users connected to every meta gateway, one slice per gateway service id (written via IServiceSlicedState).
// Registered as a deploy-scoped state so that IDeployCleanup removes the rows of old deploys. The rows hold an
// AddressableStateValue (DeploymentState), the state name is the one DeploymentState.CreateDefaultStateIdentity gives.
[GenerateSerializer]
[GrainState(Table = "cluster", State = "deployment_state_ConnectedUsersLiveData", Lookup = "ConnectedUsersLive", Key = GrainKeyType.Guid)]
public class ConnectedUsersLiveData : IServiceSlices<List<ConnectedUserEntry>>, IDirectStateValue, IDeployScopedState
{
    [Id(2)] public Dictionary<Guid, List<ConnectedUserEntry>> Slices { get; set; } = new();

    public int Version => 0;

    public List<ConnectedUserEntry> AllUsers()
    {
        return Slices.Values.SelectMany(slice => slice).ToList();
    }
}

[GenerateSerializer]
public class ConnectedUserEntry
{
    [Id(0)] public Guid UserId { get; set; }
}
