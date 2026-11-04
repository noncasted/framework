using Global.Telemetry;
using Internal;

namespace Global.FrameRate
{
    // A change of the automatic frame rate on the time line of the open session: fps.auto.<limit>. The limit itself is
    // settings.target_fps of the standard collectors.
    public class FrameRateTelemetryMarks : IScopeSetup
    {
        public FrameRateTelemetryMarks(IClientTelemetry telemetry, IAutoFrameRate auto)
        {
            _telemetry = telemetry;
            _auto = auto;
        }

        private readonly IClientTelemetry _telemetry;
        private readonly IAutoFrameRate _auto;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _auto.Switched.Advise(lifetime, target => _telemetry.Mark("fps.auto." + target));
        }
    }
}
