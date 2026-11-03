using System;
using Cysharp.Threading.Tasks;

namespace Global.Telemetry
{
    // The way of the telemetry to the server, in the structures of the module: what they become on the wire is the business
    // of the implementation, registered by the game next to AddClientTelemetry. The module makes one call at a time and
    // repeats nothing. Every call ends: an answer, or a failure when no answer comes or the connection is lost. A failure
    // the implementation expects is the outcome Failed, or an OperationCanceledException; any other exception counts as a
    // bug and gets one line in the log. Either way the session sends nothing more.
    public interface ITelemetryTransport
    {
        // The server gives the id of the session, or refuses (Disabled: the flag is off).
        UniTask<TelemetrySessionAnswer> Start(TelemetrySessionStart start);

        UniTask<TelemetrySendOutcome> Send(Guid sessionId, TelemetryBatch batch);

        UniTask End(Guid sessionId);

        // The open connection, null while none is open. Compared by reference: another value means another connection, and
        // what the server gave on the previous one (the id of a startup record) is forgotten there.
        object Link { get; }

        // Needs no session.
        UniTask<TelemetryStartupAnswer> SendStartup(TelemetryStartupTrace trace);
    }
}
