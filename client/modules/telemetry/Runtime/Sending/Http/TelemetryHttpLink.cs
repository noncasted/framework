using System;

namespace Global.Telemetry
{
    // The connection of a client over HTTP: an id of its own, sent with every request. The server keeps what it gave (the session,
    // the startup record) on this id, as on a socket, and ends it after a silence (CONTRACT.md, "HTTP").
    public class TelemetryHttpLink
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}
