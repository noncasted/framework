using Common.Extensions;
using Internal;

namespace Console.Infrastructure.ClientTelemetry;

// A page that reads the telemetry again while its component lives: the loop is the top of a task nobody awaits.
public abstract class TelemetryLivePage : UiComponent
{
    protected TelemetryLivePage()
    {
        _loop = new TelemetryRefreshLoop(Refresh, () => KeepsRefreshing, () => InvokeAsync(StateHasChanged), _interval);
    }

    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(2);

    private readonly TelemetryRefreshLoop _loop;

    protected string? RefreshError => _loop.Error;

    protected virtual bool KeepsRefreshing => true;

    protected abstract Task Refresh();

    protected override async Task OnSetup(IReadOnlyLifetime lifetime)
    {
        await _loop.Refresh();
        _loop.Run(lifetime).NoAwait();
    }
}
