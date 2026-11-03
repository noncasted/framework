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

    // The marks of a load carry its key and time: kept the way a batch keeps them, a detail scrubbed and cut, a negative time dropped.
    [Fact]
    public void TheDetailAndDuration_AreKeptLikeTheOnesOfABatch()
    {
        var parsed = TelemetryBatchParser.ParsePrevious(new SharedClientTelemetry.PreviousSession
        {
            SessionId = Guid.NewGuid(),
            LastSampleTime = -1,
            SinceMs = -1,
            Marks =
            [
                new SharedClientTelemetry.Mark { Time = 1000, Name = "load.start", Detail = "Group: MusicRunAudio" },
                new SharedClientTelemetry.Mark { Time = 6000, Name = "load.slow", Detail = "Group: MusicRunAudio", DurationMs = 5000 },
                new SharedClientTelemetry.Mark { Time = 7000, Name = "load.failed", Detail = "user@mail.com " + new string('x', 200), DurationMs = 0 },
                new SharedClientTelemetry.Mark { Time = 8000, Name = "load.end", Detail = "Group: MenuAudio", DurationMs = -5 },
                new SharedClientTelemetry.Mark { Time = 9000, Name = "page.pagehide", Detail = "a\nb" }
            ]
        });

        var marks = parsed.Session!.Marks;
        marks.Select(mark => mark.Name).Should().Equal("load.start", "load.slow", "load.failed", "page.pagehide");
        marks[0].Should().Be(new TelemetryMark(1000, "load.start", "Group: MusicRunAudio"));
        marks[1].Should().Be(new TelemetryMark(6000, "load.slow", "Group: MusicRunAudio", 5000));
        marks[2].Detail!.Length.Should().BeLessThanOrEqualTo(ClientTelemetryLimits.MaxMarkDetailLength);
        marks[2].Detail.Should().NotContain("user@mail.com");
        marks[2].DurationMs.Should().Be(0);
        marks[3].Detail.Should().BeNull();
        parsed.Rejected.Should().Be(1);
    }
}
