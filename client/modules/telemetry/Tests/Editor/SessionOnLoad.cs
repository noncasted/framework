using Internal;

namespace Global.Telemetry.Tests
{
    // What a game does: opens a session when it gets to a part of the game, here on the lifetime of the scope.
    public class SessionOnLoad
    {
        public SessionOnLoad(IClientTelemetry telemetry)
        {
            _telemetry = telemetry;
        }

        private readonly IClientTelemetry _telemetry;

        public void Enter(IReadOnlyLifetime lifetime)
        {
            _telemetry.StartSession(lifetime, "probe");
        }
    }
}
