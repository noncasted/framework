using Console.Infrastructure.ClientTelemetry;
using Game.ClientTelemetry;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Framework.Tests.ClientTelemetry.TelemetryConsoleSamples;

namespace Framework.Tests.ClientTelemetry;

// The markup of the small pieces of the pages, without a circuit.
public class TelemetryConsoleViewTests
{
    [Fact]
    public async Task Row_ShowsEveryTagOfTheSession()
    {
        var html = await Render<TelemetrySessionRow>(new() { [nameof(TelemetrySessionRow.Session)] = SessionOf(tags: ["alpha", "beta.two"]) });

        Assert.Contains(">alpha<", html);
        Assert.Contains(">beta.two<", html);
    }

    [Fact]
    public async Task Row_WithoutTags_ShowsTheUnknownMark()
    {
        var html = await Render<TelemetryTagList>(new() { [nameof(TelemetryTagList.Tags)] = Array.Empty<string>() });

        Assert.Contains($">{TelemetryValueFormat.Unknown}<", html);
    }

    [Fact]
    public async Task LostBadge_SaysItDoesNotProveADeadClient()
    {
        var lost = await Render<TelemetryStatusBadge>(new() { [nameof(TelemetryStatusBadge.Status)] = TelemetrySessionStatus.Lost });
        var ended = await Render<TelemetryStatusBadge>(new() { [nameof(TelemetryStatusBadge.Status)] = TelemetrySessionStatus.Ended });

        Assert.Contains($"title=\"{TelemetrySessionFacts.LostMeaning}\"", lost);
        Assert.Contains("socket", TelemetrySessionFacts.LostMeaning);
        Assert.Contains("timer", TelemetrySessionFacts.LostMeaning);
        Assert.DoesNotContain("title=", ended);
    }

    private static async Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }
}
