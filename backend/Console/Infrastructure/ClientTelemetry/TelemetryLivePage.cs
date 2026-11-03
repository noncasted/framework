using Common.Extensions;
using Internal;

namespace Console.Infrastructure.ClientTelemetry;

// A page that reads the telemetry again while its component lives. A failed read is shown and the next one goes on: this loop is
// the top of a task nobody awaits.
public abstract class TelemetryLivePage : UiComponent
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(2);

    protected string? RefreshError { get; private set; }

    protected virtual bool KeepsRefreshing => true;

    protected abstract Task Refresh();

    protected override async Task OnSetup(IReadOnlyLifetime lifetime)
    {
        await RefreshGuarded();
        RefreshLoop(lifetime).NoAwait();
    }

    private async Task RefreshLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false && KeepsRefreshing)
        {
            await Task.Delay(_interval, lifetime.Token);
            await RefreshGuarded();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RefreshGuarded()
    {
        try
        {
            await Refresh();
            RefreshError = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RefreshError = exception.Message;
        }
    }
}
