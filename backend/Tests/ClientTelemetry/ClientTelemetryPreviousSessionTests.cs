using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.ClientTelemetry;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// A session that did not close on the client, as its next session reports it: kept with the new one and found from the old one.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetryPreviousSessionTests : ClientTelemetryStorageTest
{
    public ClientTelemetryPreviousSessionTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    private readonly IClientTelemetrySwitch _switch = Substitute.For<IClientTelemetrySwitch>();

    private ClientTelemetryConnection Open()
    {
        _switch.IsEnabled.Returns(true);

        return new ClientTelemetryConnections(_switch, Cluster.Orleans, NullLogger<ClientTelemetryConnection>.Instance).Of(new Lifetime());
    }

    private static SharedClientTelemetry.PreviousSession Previous(Guid id)
    {
        return new SharedClientTelemetry.PreviousSession
        {
            SessionId = id,
            Tags = ["menu"],
            LastSampleTime = 31_000,
            SinceMs = 4_000,
            Marks =
            [
                new SharedClientTelemetry.Mark { Time = 30_500, Name = "webgl.error.out_of_memory" },
                new SharedClientTelemetry.Mark { Time = 31_200, Name = "page.pagehide" }
            ]
        };
    }

    [Fact]
    public async Task ThePreviousSession_IsKeptWithTheNewOne_AndFindsIt()
    {
        var connection = Open();
        var lost = (await connection.Start(new SharedClientTelemetry.SessionStart())).SessionId;
        var next = (await Open().Start(new SharedClientTelemetry.SessionStart { Previous = Previous(lost) })).SessionId;

        var previous = (await Session(next)).Previous!;

        previous.SessionId.Should().Be(lost);
        previous.Tags.Should().Equal("menu");
        previous.LastSampleTime.Should().Be(31_000);
        previous.SinceMs.Should().Be(4_000);
        previous.Marks.Select(mark => (mark.Time, mark.Name)).Should().Equal((30_500L, "webgl.error.out_of_memory"), (31_200L, "page.pagehide"));
        (await Session(lost)).Previous.Should().BeNull();

        var found = await Reader.GetSessions(new TelemetrySessionFilter { PreviousId = lost });
        found.Select(item => item.Id).Should().Equal(next);
    }
}
