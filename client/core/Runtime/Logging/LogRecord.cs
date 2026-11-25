using System;

namespace Internal
{
    // What a sink receives: one call of a log whose level was on. IsShown: at or above the level of the category (the
    // console shows it), IsMark: at or above its mark level. Template and Message are null for an exception
    // logged without a text.
    public readonly struct LogRecord
    {
        public LogRecord(string category, LogLevel level, string template, string message, Exception exception, bool isShown, bool isMark)
        {
            Category = category;
            Level = level;
            Template = template;
            Message = message;
            Exception = exception;
            IsShown = isShown;
            IsMark = isMark;
        }

        public string Category { get; }
        public LogLevel Level { get; }
        public string Template { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public bool IsShown { get; }
        public bool IsMark { get; }
    }
}
