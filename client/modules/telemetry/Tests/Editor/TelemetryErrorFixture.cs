using System.Collections.Generic;
using Internal;

namespace Global.Telemetry.Tests
{
    // The way of an error up to ITelemetryErrors, with a manual clock and a recording end.
    public class TelemetryErrorFixture
    {
        public TelemetryErrorFixture(ITelemetryErrors errors = null)
        {
            Records = new RecordingErrors();
            Options = new ClientTelemetryOptions(enabledInEditor: true);
            Queue = new TelemetryErrorQueue();
            Groups = new TelemetryErrorGroups(errors ?? Records, Clock);
            Pump = new TelemetryErrorPump(Queue, new TelemetryTextCleaner(), Groups, Options);
            Logs = new TelemetryLogErrors(Queue);
            Logger = new TelemetryLoggerErrors(Queue);
        }

        public ManualClock Clock { get; } = new ManualClock();
        public Lifetime Running { get; } = new Lifetime();
        public RecordingErrors Records { get; }
        public ClientTelemetryOptions Options { get; }
        public TelemetryErrorQueue Queue { get; }
        public TelemetryErrorGroups Groups { get; }
        public TelemetryErrorPump Pump { get; }
        public TelemetryLogErrors Logs { get; }
        public TelemetryLoggerErrors Logger { get; }

        public void Listen()
        {
            Logs.Start(Running);
        }

        // Both ways of the errors of a game: the log of Unity and the logger of the framework.
        public void ListenWithLogger()
        {
            Logs.Start(Running);
            Logger.Start(Running);
        }

        public void PumpAt(long time)
        {
            Clock.Set(time);
            Pump.Pump();
        }
    }

    public class RecordingErrors : ITelemetryErrors
    {
        public List<ErrorEntry> Entries { get; } = new List<ErrorEntry>();

        public void Add(TelemetryErrorLevel level, string category, string message, string stack, int repeat)
        {
            Entries.Add(new ErrorEntry(level, category, message, stack, repeat));
        }
    }

    public class ErrorEntry
    {
        public ErrorEntry(TelemetryErrorLevel level, string category, string message, string stack, int repeat)
        {
            Level = level;
            Category = category;
            Message = message;
            Stack = stack;
            Repeat = repeat;
        }

        public TelemetryErrorLevel Level { get; }
        public string Category { get; }
        public string Message { get; }
        public string Stack { get; }
        public int Repeat { get; }
    }
}
