using FluentAssertions;
using Infrastructure.Startup;
using Xunit;

namespace Framework.Tests.Cluster;

public class ClusterParticipantContextTests
{
    [Fact]
    public void SetStage_Listed_BecomesCurrent()
    {
        var context = new ClusterParticipantContext();
        context.SetStages(["Orleans", "Messaging"]);

        context.SetStage("Messaging");

        context.CurrentStageIndex.Value.Should().Be(1);
    }

    [Fact]
    public void SetStage_Unknown_Throws()
    {
        var context = new ClusterParticipantContext();
        context.SetStages(["Orleans", "Messaging"]);

        var set = () => context.SetStage("Missing");

        set.Should().Throw<InvalidOperationException>().WithMessage("*Missing*");
    }
}
