using Internal;

namespace Global.Telemetry.Tests.Transport
{
    // The branch of WebSocketFactory in the editor, without the options of a platform.
    public class TelemetryEditorSockets : IWebSocketFactory
    {
        public IWebSocket Create(string url, IReadOnlyLifetime lifetime)
        {
            return new DefaultWebSocket(url, lifetime);
        }
    }
}
