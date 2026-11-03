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

        // A collector of a scope below the one of the telemetry (a run under the meta): sampled in every session while the
        // lifetime lives, from the next sample on. One that is also ITelemetryLiveCollector is started in the open session and
        // in each later one, with a lifetime that ends with the session or with this one. Nothing is called with the telemetry off.
        void AddCollector(IReadOnlyLifetime lifetime, ITelemetryCollector collector);

        // Sends the trace of the start of the game: what it is now, and the final one when it closes; a closed one goes at once.
        // Call it after every authorization of the meta socket, next to the first StartSession and before it: the session
        // refers to the record of the trace, and the id of a record lives as long as the connection. Before the socket opens, or
        // when the traces of this connection are over, nothing goes and the log gets one line.
        void SendStartupTrace();
    }
}
