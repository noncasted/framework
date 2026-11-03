using Internal;

namespace Global.Telemetry
{
    // Main thread only.
    public interface IClientTelemetry
    {
        // Opens a session: a part of the game named by its tags, which come from the code of the game (see TelemetryNameGuard).
        // Nothing is collected or sent without one, the module opens none by itself. The end of the lifetime closes the
        // session, a new start closes the one still open.
        void StartSession(IReadOnlyLifetime lifetime, params string[] tags);

        // A moment of the game on the time line of the open session. The name comes from the code: see TelemetryNameGuard.
        void Mark(string name);

        // Sends the trace of the start of the game: what it is now, and the final one when it closes; a closed one goes at once.
        // Call it after every authorization of the meta socket, next to the first StartSession and before it: the session
        // refers to the record of the trace, and the id of a record lives as long as the connection. Before the socket opens, or
        // when the traces of this connection are over, nothing goes and the log gets one line.
        void SendStartupTrace();
    }
}
