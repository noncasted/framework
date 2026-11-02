using Common;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;

namespace Cluster.Deploy;

[GenerateSerializer]
[GrainState(Table = "cluster", State = "deploy", Lookup = "Deploy", Key = GrainKeyType.Guid)]
public class DeployState : IDirectStateValue, IDeployScopedState
{
    [Id(0)] [JsonProperty] public Guid DeployId { get; private set; }
    [Id(1)] [JsonProperty] public DateTime LastHeartbeat { get; private set; }
    [Id(2)] [JsonProperty] public bool CoordinatorReady { get; private set; }

    public int Version => 0;

    public void Init(Guid deployId, DateTime now)
    {
        DeployId = deployId;
        LastHeartbeat = now;
        CoordinatorReady = false;
    }

    public void Heartbeat(DateTime now)
    {
        LastHeartbeat = now;
    }

    public void MarkCoordinatorReady(DateTime now)
    {
        CoordinatorReady = true;
        LastHeartbeat = now;
    }
}