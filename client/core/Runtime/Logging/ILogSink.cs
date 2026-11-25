namespace Internal
{
    // A receiver of the records next to the console (the client telemetry). Called on the thread that wrote the log; a sink
    // must not write to a log itself.
    public interface ILogSink
    {
        void Write(in LogRecord record);
    }
}
