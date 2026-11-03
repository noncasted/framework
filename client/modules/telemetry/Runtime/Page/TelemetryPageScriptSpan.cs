using System;

namespace Global.Telemetry
{
    // A span of the template of the game (window.startupTrace, WebTemplate~/startup-trace.js). parent: the index of the span open
    // when it began, -1 for none; end: -1 while it is open.
    [Serializable]
    public class TelemetryPageScriptSpan
    {
        public string name;
        public int parent;
        public double start;
        public double end;
    }
}
