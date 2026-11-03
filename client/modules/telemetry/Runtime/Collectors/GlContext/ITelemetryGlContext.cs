using Internal;

namespace Global.Telemetry
{
    // The WebGL context of the canvas of the game. What happened is kept on the page until taken, whatever is open on the managed
    // side, so a loss without a session waits for the next one and is taken once however many scopes come and go.
    public interface ITelemetryGlContext
    {
        bool IsLost { get; }

        // Any event of the context.
        IEventSource Changed { get; }

        // The events since the previous take.
        int TakeLost();
        int TakeRestored();
    }
}
