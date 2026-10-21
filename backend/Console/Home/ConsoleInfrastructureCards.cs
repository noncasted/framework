using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Console;

// A card of the project in the Infrastructure section (home and infrastructure dashboard): benchmarks, its own
// infrastructure pages. Shown before the framework cards, in registration order.
public class ConsoleInfrastructureCard
{
    public required string Icon { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Target { get; init; }
}

public static class ConsoleInfrastructureCardsExtensions
{
    public static IHostApplicationBuilder AddConsoleInfrastructureCard(
        this IHostApplicationBuilder builder,
        ConsoleInfrastructureCard card)
    {
        builder.Services.AddSingleton(card);
        return builder;
    }
}
