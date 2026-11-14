using Cluster.Discovery;
using Cluster.Lobbies;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Cluster;

// The lobbies of a game server that stopped without releasing them (a crash) leave the list of open lobbies once the
// server is gone from the discovery of the deploy, not only at the end of the deploy.
[Collection(nameof(OrleansIntegrationCollection))]
public class LobbyRegistryLivenessTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task OpenLobbies_ServerGoneFromDiscovery_ItsLobbiesAreNotListed()
    {
        var deployId = Guid.NewGuid();
        var registry = GetGrain<ILobbyRegistry>(deployId);
        var discovery = GetGrain<IServiceDiscoveryStorage>(deployId);

        var alive = Guid.NewGuid();
        var crashed = Guid.NewGuid();
        await Announce(discovery, alive);
        await Announce(discovery, crashed);

        var aliveCode = await List(registry, alive);
        await List(registry, crashed);

        (await registry.GetOpenLobbies()).Entries.Should().HaveCount(2);

        await discovery.Unregister(crashed);

        var open = await registry.GetOpenLobbies();
        open.Entries.Select(entry => entry.Code).Should().Equal(aliveCode);
    }

    private static Task Announce(IServiceDiscoveryStorage discovery, Guid serverId)
    {
        return discovery.Update(new GameServerOverview
        {
            Id = serverId,
            Tag = TestRoles.Game,
            UpdateTime = DateTime.UtcNow,
            Url = "http://server"
        });
    }

    private static async Task<string> List(ILobbyRegistry registry, Guid serverId)
    {
        var sessionId = Guid.NewGuid();
        var code = await registry.Register(serverId, sessionId);

        await registry.UpdateListing(code, sessionId, new LobbyListing
        {
            IsOpen = true,
            IsJoinable = true,
            LeaderUserId = Guid.NewGuid(),
            PlayersCount = 1,
            MaxPlayers = 4
        });

        return code;
    }
}
