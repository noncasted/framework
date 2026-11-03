using MemoryPack;
using Shared;

namespace Framework.Tests.Protocol;

// The union over INetworkContext as a project declares it (its NetworkContextUnion): every framework type with its
// tag, no game types. The module initializer of the generated formatter registers it before the first message.
// A framework type without a tag here fails ProtocolSchemaTests.EveryImplementation_HasUnionTag.
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
[MemoryPackUnion(NetworkContextTags.SessionSearch, typeof(SharedSessionRequests.Search))]
[MemoryPackUnion(NetworkContextTags.SessionResult, typeof(SharedSessionRequests.Result))]
[MemoryPackUnion(NetworkContextTags.LobbyCreate, typeof(SharedLobbies.CreateLobby))]
[MemoryPackUnion(NetworkContextTags.LobbyJoin, typeof(SharedLobbies.JoinLobby))]
[MemoryPackUnion(NetworkContextTags.SessionTicketRequest, typeof(SharedSessionRequests.TicketRequest))]
[MemoryPackUnion(NetworkContextTags.SessionTicketResponse, typeof(SharedSessionRequests.TicketResponse))]
[MemoryPackUnion(NetworkContextTags.LobbiesSubscribe, typeof(SharedLobbies.Subscribe))]
[MemoryPackUnion(NetworkContextTags.LobbiesUnsubscribe, typeof(SharedLobbies.Unsubscribe))]
[MemoryPackUnion(NetworkContextTags.LobbiesList, typeof(SharedLobbies.List))]
[MemoryPackUnion(NetworkContextTags.LobbyJoinMatch, typeof(SharedLobbies.JoinMatch))]
[MemoryPackUnion(NetworkContextTags.SessionCreateSolo, typeof(SharedSessionRequests.CreateSolo))]
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

// The payload unions a project declares next to its payload types: here only the framework members.
[MemoryPackUnionFormatter(typeof(IEntityPayload))]
[MemoryPackUnion(0, typeof(TestEntityPayload))]
public partial class TestEntityPayloadUnion
{
}

[MemoryPackUnionFormatter(typeof(IStatePayload))]
[MemoryPackUnion(StatePayloadTags.Empty, typeof(EmptyStatePayload))]
public partial class TestStatePayloadUnion
{
}

[MemoryPackUnionFormatter(typeof(IEventPayload))]
[MemoryPackUnion(0, typeof(TestEventPayload))]
public partial class TestEventPayloadUnion
{
}

[MemoryPackable]
public partial class TestEntityPayload : IEntityPayload
{
    public int Kind { get; set; }
}

[MemoryPackable]
public partial class TestEventPayload : IEventPayload
{
    public int Value { get; set; }
}
