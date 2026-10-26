using MemoryPack;
using NUnit.Framework;
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
    public partial class TestNetworkContextUnion
    {
    }

    // The payloads of the entities: a union over IEntityPayload is declared by the game as well.
    [MemoryPackUnionFormatter(typeof(IEntityPayload))]
    [MemoryPackUnion(0, typeof(TestEntityPayload))]
    public partial class TestEntityPayloadUnion
    {
    }

    [MemoryPackable]
    public partial class TestEntityPayload : IEntityPayload
    {
    }

    [SetUpFixture]
    public class TestNetworkProtocol
    {
        [OneTimeSetUp]
        public void Register()
        {
            // In a game project its own union is registered on load and covers these tags plus the game ones:
            // replacing it would break the tests of the game that run after these.
            if (MemoryPackFormatterProvider.IsRegistered<INetworkContext>() == false)
                TestNetworkContextUnionInitializer.RegisterFormatter();

            if (MemoryPackFormatterProvider.IsRegistered<IEntityPayload>() == false)
                TestEntityPayloadUnionInitializer.RegisterFormatter();
        }
    }
}
