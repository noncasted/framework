using Microsoft.Extensions.Logging;

namespace Framework.Tests.State.Storages;

// Keeps what a storage logged: the storages log on their own category, a test sees the level and the text.
public class RecordingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _entries = new();

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_entries)
                return _entries.ToList();
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (_entries)
            _entries.Add((logLevel, formatter(state, exception)));
    }
}
