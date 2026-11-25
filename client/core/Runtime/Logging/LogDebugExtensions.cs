using System.Diagnostics;
using UnityEngine;

namespace Internal
{
    // Debug of a log: the level is checked before the template is formatted, so a level that is off costs one check and
    // allocates nothing (the arguments are generic: no boxing).
    // Cut out by the compiler where the build has none of the symbols (a release player without FRAMEWORK_LOG_DEBUG):
    // the call and its arguments are not there at all.
    public static class LogDebugExtensions
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("FRAMEWORK_LOG_DEBUG")]
        [HideInCallstack]
        public static void Debug(this ILog log, string template)
        {
            if (log.IsEnabled(LogLevel.Debug))
                log.Write(LogLevel.Debug, template, LogTemplate.Format(template), null);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("FRAMEWORK_LOG_DEBUG")]
        [HideInCallstack]
        public static void Debug<T1>(this ILog log, string template, T1 a)
        {
            if (log.IsEnabled(LogLevel.Debug))
                log.Write(LogLevel.Debug, template, LogTemplate.Format(template, a), null);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("FRAMEWORK_LOG_DEBUG")]
        [HideInCallstack]
        public static void Debug<T1, T2>(this ILog log, string template, T1 a, T2 b)
        {
            if (log.IsEnabled(LogLevel.Debug))
                log.Write(LogLevel.Debug, template, LogTemplate.Format(template, a, b), null);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("FRAMEWORK_LOG_DEBUG")]
        [HideInCallstack]
        public static void Debug<T1, T2, T3>(this ILog log, string template, T1 a, T2 b, T3 c)
        {
            if (log.IsEnabled(LogLevel.Debug))
                log.Write(LogLevel.Debug, template, LogTemplate.Format(template, a, b, c), null);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("FRAMEWORK_LOG_DEBUG")]
        [HideInCallstack]
        public static void Debug<T1, T2, T3, T4>(this ILog log, string template, T1 a, T2 b, T3 c, T4 d)
        {
            if (log.IsEnabled(LogLevel.Debug))
                log.Write(LogLevel.Debug, template, LogTemplate.Format(template, a, b, c, d), null);
        }
    }
}
