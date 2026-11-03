using System;
using Internal;

namespace Global.Telemetry
{
    // The first trace of the run of the game, the one of its start. The traces after it (the next runs of a match) are not its.
    public interface ITelemetryStartupSource
    {
        // The closed trace, null while it is open or never began.
        ProfilerTraceData ReadFinal();

        // The open trace as it is now, null when it never began. The trace goes on untouched.
        ProfilerTraceData ReadSnapshot();

        // Called when the trace closes.
        void ListenFinal(IReadOnlyLifetime lifetime, Action listener);
    }
}
