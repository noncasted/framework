using FluentAssertions;
using Game.ClientTelemetry;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The client is not trusted here either: what is out of form is dropped alone and counted, the start goes on.
public class TelemetryPreviousSessionParsingTests
{
    [Fact]
    public void None_IsNull()
    {
        TelemetryBatchParser.ParsePrevious(null).Session.Should().BeNull();
    }

    [Fact]
    public void AnEmptyId_DropsTheWhole_AndCountsIt()
    {
        var parsed = TelemetryBatchParser.ParsePrevious(new SharedClientTelemetry.PreviousSession { Tags = ["menu"] });

        parsed.Session.Should().BeNull();
        parsed.Rejected.Should().Be(1);
    }

    [Fact]
    public void BadParts_AreDroppedAlone()
    {
        var marks = Enumerable.Range(0, ClientTelemetryLimits.MaxPreviousMarks + 2)
                              .Select(index => new SharedClientTelemetry.Mark { Time = index, Name = $"mark.{index}" })
                              .Append(new SharedClientTelemetry.Mark { Time = 1, Name = "Player Ivan" })
                              .Append(new SharedClientTelemetry.Mark { Time = -1, Name = "page.freeze" })
                              .Append(null!)
                              .ToArray();

        var parsed = TelemetryBatchParser.ParsePrevious(new SharedClientTelemetry.PreviousSession
        {
            SessionId = Guid.NewGuid(),
            Tags = ["run", "Bad Tag"],
            LastSampleTime = -5,
            SinceMs = -1,
            Marks = marks
        });

        var session = parsed.Session!;
        session.Tags.Should().Equal("run");
        session.LastSampleTime.Should().BeNull();
        session.SinceMs.Should().BeNull();
        session.Marks.Should().HaveCount(ClientTelemetryLimits.MaxPreviousMarks);
        session.Marks.Select(mark => mark.Name).Should().OnlyContain(name => name.StartsWith("mark."));
        session.Marks[^1].Name.Should().Be($"mark.{ClientTelemetryLimits.MaxPreviousMarks + 1}");
        parsed.Rejected.Should().BeGreaterThan(0);
    }
}
