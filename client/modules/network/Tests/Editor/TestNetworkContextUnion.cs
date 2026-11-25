using MemoryPack;
using Shared;

namespace Internal.Tests
{
    // The union over INetworkContext is declared by the game (see INetworkContext): the framework project has none,
    // so the tests that serialize messages declare one with the framework tags.
    [MemoryPackUnionFormatter(typeof(INetworkContext))]
    [MemoryPackUnion(NetworkContextTags.EmptyResponse, typeof(EmptyResponse))]
    [MemoryPackUnion(NetworkContextTags.ErrorResponse, typeof(ErrorResponse))]
    [MemoryPackUnion(NetworkContextTags.SessionAuthRequest, typeof(SharedSessionAuth.Request))]
    [MemoryPackUnion(NetworkContextTags.SessionAuthResponse, typeof(SharedSessionAuth.Response))]
    [MemoryPackUnion(NetworkContextTags.EntityCreateRequest, typeof(SharedSessionEntity.CreateRequest))]
    [MemoryPackUnion(NetworkContextTags.EntityCreateResponse, typeof(SharedSessionEntity.CreateResponse))]
    [MemoryPackUnion(NetworkContextTags.EntityCreatedOverview, typeof(SharedSessionEntity.CreatedOverview))]
    [MemoryPackUnion(NetworkContextTags.EntityDestroy, typeof(SharedSessionEntity.Destroy))]
    [MemoryPackUnion(NetworkContextTags.EntityDestroyUpdate, typeof(SharedSessionEntity.DestroyUpdate))]
    [MemoryPackUnion(NetworkContextTags.ObjectSetProperty, typeof(SharedSessionObject.SetProperty))]
    [MemoryPackUnion(NetworkContextTags.ObjectPropertyUpdate, typeof(SharedSessionObject.PropertyUpdate))]
    [MemoryPackUnion(NetworkContextTags.ObjectEvent, typeof(SharedSessionObject.Event))]
    [MemoryPackUnion(NetworkContextTags.ObjectSetProperties, typeof(SharedSessionObject.SetProperties))]
    [MemoryPackUnion(NetworkContextTags.PlayerLocalUpdate, typeof(SharedSessionPlayer.LocalUpdate))]
    [MemoryPackUnion(NetworkContextTags.PlayerRemoteUpdate, typeof(SharedSessionPlayer.RemoteUpdate))]
    [MemoryPackUnion(NetworkContextTags.PlayerRemoteDisconnect, typeof(SharedSessionPlayer.RemoteDisconnect))]
    [MemoryPackUnion(NetworkContextTags.PlayerRemoteAway, typeof(SharedSessionPlayer.RemoteAway))]
    [MemoryPackUnion(NetworkContextTags.PlayerLeave, typeof(SharedSessionPlayer.Leave))]
    [MemoryPackUnion(NetworkContextTags.ServiceGetRequest, typeof(SharedSessionService.GetRequest))]
    [MemoryPackUnion(NetworkContextTags.ServiceGetResponse, typeof(SharedSessionService.GetResponse))]
    [MemoryPackUnion(NetworkContextTags.ServiceOverview, typeof(SharedSessionService.Overview))]
    [MemoryPackUnion(NetworkContextTags.LatencyPing, typeof(SharedSessionLatency.Ping))]
    [MemoryPackUnion(NetworkContextTags.LatencyPong, typeof(SharedSessionLatency.Pong))]
    [MemoryPackUnion(NetworkContextTags.LatencyPeerProbe, typeof(SharedSessionLatency.PeerProbe))]
    [MemoryPackUnion(NetworkContextTags.LatencyPeerEcho, typeof(SharedSessionLatency.PeerEcho))]
    [MemoryPackUnion(NetworkContextTags.PeerSignal, typeof(SharedSessionPeer.Signal))]
    [MemoryPackUnion(NetworkContextTags.PeerTunnel, typeof(SharedSessionPeer.Tunnel))]
    [MemoryPackUnion(NetworkContextTags.TelemetrySessionStart, typeof(SharedClientTelemetry.SessionStart))]
    [MemoryPackUnion(NetworkContextTags.TelemetrySessionStarted, typeof(SharedClientTelemetry.SessionStarted))]
    [MemoryPackUnion(NetworkContextTags.TelemetryBatch, typeof(SharedClientTelemetry.Batch))]
    [MemoryPackUnion(NetworkContextTags.TelemetryBatchResult, typeof(SharedClientTelemetry.BatchResult))]
    [MemoryPackUnion(NetworkContextTags.TelemetrySessionEnd, typeof(SharedClientTelemetry.SessionEnd))]
    [MemoryPackUnion(NetworkContextTags.TelemetryStartupTrace, typeof(SharedClientTelemetry.StartupTrace))]
    [MemoryPackUnion(NetworkContextTags.TelemetryStartupTraceResult, typeof(SharedClientTelemetry.StartupTraceResult))]
    public partial class TestNetworkContextUnion
    {
    }
}
