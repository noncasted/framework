using Internal;

namespace Global.Telemetry
{
    // The socket of the meta the telemetry goes through: the game registers its meta backend as this, in the scope of
    // AddSocketClientTelemetry or above it. Read on every request: a socket opened again may be a new connection.
    //
    // The connection must be authorized before the game opens a session: an open connection takes a request at once, and the
    // meta expects its authorization as the first frame.
    public interface ITelemetrySocket
    {
        INetworkConnection Connection { get; }
    }
}
