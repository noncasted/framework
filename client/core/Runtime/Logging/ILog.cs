using System;

namespace Internal
{
    // The log of a category. Written through LogExtensions (Debug, Info, Warning, Error, Exception): they check the level
    // before anything is formatted. Any thread.
    public interface ILog
    {
        // The full name of the type the log belongs to, matched by the rules of LogOptions.
        string Category { get; }

        bool IsEnabled(LogLevel level);

        // The template is the text with its {Name} placeholders, the message is the template with the arguments in them;
        // both null for an exception without a text of its own (LogErrorExtensions.Exception).
        void Write(LogLevel level, string template, string message, Exception exception);
    }

    // The log of the type T: a parameter of a constructor or of an [Inject] method, given by every scope (no registration).
    // A static class, which cannot be a type argument, takes Logs.Of(typeof(TheClass)).
    public interface ILog<T> : ILog
    {
    }
}
