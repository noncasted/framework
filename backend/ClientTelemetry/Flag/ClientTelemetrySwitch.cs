using Infrastructure;
using Internal;

namespace Game.ClientTelemetry;

// AddressableState keeps the value, the channel to the other hosts and the reload after a gap of the channel. Its IsInitialized does
// not tell "loaded": it stays false for a row that was never written, and off is the default of that row.
public class ClientTelemetrySwitch : IClientTelemetrySwitch, IOrleansStarted
{
    public ClientTelemetrySwitch(AddressableStateUtils utils)
    {
        _state = new AddressableState<ClientTelemetrySettingsState>(utils);
    }

    private readonly AddressableState<ClientTelemetrySettingsState> _state;
    private readonly ViewableProperty<bool> _isLoaded = new(false);

    public bool IsLoaded => _isLoaded.Value;
    public bool IsEnabled => _state.Value.IsEnabled;

    // A failed read stops the start of the host, like any AddressableState.
    public async Task OnOrleansStarted(IReadOnlyLifetime lifetime)
    {
        await _state.OnOrleansStarted(lifetime);
        _isLoaded.Set(true);
    }

    public void View(IReadOnlyLifetime lifetime, Action listener)
    {
        listener();
        _isLoaded.Advise(lifetime, (_, _) => listener());
        _state.Advise(lifetime, (_, _) => listener());
    }

    public Task Set(bool enabled)
    {
        if (!IsLoaded)
            throw new InvalidOperationException("The client telemetry switch is not loaded yet");

        return _state.SetValue(new ClientTelemetrySettingsState { IsEnabled = enabled });
    }
}
