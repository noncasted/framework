using Internal;

namespace Global.Telemetry.Tests.Transport
{
    // What the code of a game does when it gets to a part of the game, on the authorized socket.
    public class TelemetryTransportGameSession
    {
        public TelemetryTransportGameSession(IClientTelemetry telemetry)
        {
            _telemetry = telemetry;
        }

        private readonly IClientTelemetry _telemetry;

        public void Enter(IReadOnlyLifetime lifetime)
        {
            _telemetry.SendStartupTrace();
            _telemetry.StartSession(lifetime, TelemetryTransportRoots.Tag);
            _telemetry.Mark("game.loaded");
        }
    }
}
