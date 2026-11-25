using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // The categories of the logs, their levels and the output: the console of Unity and the sinks. Levels come from the
    // LogOptions of the OptionsContainer (Configure, called by OptionsContainer.Register); before that the defaults of
    // LogOptions hold. Any thread.
    public static class Logs
    {
        private static readonly object _lock = new();
        private static readonly Dictionary<string, LogCategory> _categories = new();
        private static LogOptions _options = new();
        private static ILogSink[] _sinks = Array.Empty<ILogSink>();

        [ThreadStatic]
        private static bool _isWriting;

        // A build of the editor or a development one takes the development levels of the options.
        private const bool _isDevelopment =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        // True on the thread that is writing a line of a log into the console of Unity: the listeners of
        // Application.logMessageReceived tell these lines from the others (the sinks got them already).
        public static bool IsWriting => _isWriting;

        public static ILog<T> Of<T>()
        {
            return Cache<T>.Log;
        }

        // For a static class, which cannot be a type argument.
        public static ILog Of(Type type)
        {
            return new CategoryLog(CategoryOf(type));
        }

        public static void Configure(LogOptions options)
        {
            lock (_lock)
            {
                _options = options;

                foreach (var category in _categories.Values)
                    Apply(category);
            }
        }

        // The sink gets every record of a level that is on, from the next one until the end of the lifetime.
        public static void AddSink(IReadOnlyLifetime lifetime, ILogSink sink)
        {
            lock (_lock)
                _sinks = With(_sinks, sink);

            lifetime.Listen(() =>
            {
                lock (_lock)
                    _sinks = Without(_sinks, sink);
            });
        }

        [HideInCallstack]
        public static void Write(LogCategory category, LogLevel level, string template, string message, Exception exception)
        {
            var isShown = level != LogLevel.None && level >= category.Shown;
            var isMark = level != LogLevel.None && level >= category.Mark;

            if (isShown == false && isMark == false)
                return;

            var wasWriting = _isWriting;
            _isWriting = true;

            try
            {
                if (isShown)
                    UnityLogOutput.Write(category, level, message, exception);

                var record = new LogRecord(category.Name, level, template, message, exception, isShown, isMark);

                foreach (var sink in _sinks)
                    WriteTo(sink, record);
            }
            finally
            {
                _isWriting = wasWriting;
            }
        }

        // A domain kept between two plays (Enter Play Mode without a reload) starts again from the defaults.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            lock (_lock)
                _sinks = Array.Empty<ILogSink>();

            Configure(new LogOptions());
        }

        private static LogCategory CategoryOf(Type type)
        {
            var name = LogNames.Of(type);

            lock (_lock)
            {
                if (_categories.TryGetValue(name, out var category))
                    return category;

                category = new LogCategory(name, LogNames.ShortOf(type));
                Apply(category);
                _categories.Add(name, category);
                return category;
            }
        }

        private static void Apply(LogCategory category)
        {
            var (shown, mark) = _options.LevelsOf(category.Name, _isDevelopment);
            category.SetLevels(shown, mark);
        }

        // The isolation of the sinks: one that throws does not stop the others nor the code that logs.
        private static void WriteTo(ILogSink sink, in LogRecord record)
        {
            try
            {
                sink.Write(record);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static ILogSink[] With(ILogSink[] sinks, ILogSink sink)
        {
            var result = new ILogSink[sinks.Length + 1];
            Array.Copy(sinks, result, sinks.Length);
            result[sinks.Length] = sink;
            return result;
        }

        private static ILogSink[] Without(ILogSink[] sinks, ILogSink sink)
        {
            var index = Array.IndexOf(sinks, sink);

            if (index < 0)
                throw new InvalidOperationException("[Log] The sink is not added");

            var result = new ILogSink[sinks.Length - 1];
            Array.Copy(sinks, 0, result, 0, index);
            Array.Copy(sinks, index + 1, result, index, sinks.Length - index - 1);
            return result;
        }

        private static class Cache<T>
        {
            public static readonly ILog<T> Log = new CategoryLog<T>(CategoryOf(typeof(T)));
        }
    }
}
