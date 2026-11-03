using System;

namespace Global.Telemetry
{
    public class TelemetryWallClock : ITelemetryWallClock
    {
        public long NowMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
