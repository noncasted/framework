using UnityEngine;

namespace Internal
{
    // Info of a log: the level is checked before the template is formatted, so a level that is off costs one check and
    // allocates nothing (the arguments are generic: no boxing).
    public static class LogInfoExtensions
    {
        [HideInCallstack]
        public static void Info(this ILog log, string template)
        {
            if (log.IsEnabled(LogLevel.Info))
                log.Write(LogLevel.Info, template, LogTemplate.Format(template), null);
        }

        [HideInCallstack]
        public static void Info<T1>(this ILog log, string template, T1 a)
        {
            if (log.IsEnabled(LogLevel.Info))
                log.Write(LogLevel.Info, template, LogTemplate.Format(template, a), null);
        }

        [HideInCallstack]
        public static void Info<T1, T2>(this ILog log, string template, T1 a, T2 b)
        {
            if (log.IsEnabled(LogLevel.Info))
                log.Write(LogLevel.Info, template, LogTemplate.Format(template, a, b), null);
        }

        [HideInCallstack]
        public static void Info<T1, T2, T3>(this ILog log, string template, T1 a, T2 b, T3 c)
        {
            if (log.IsEnabled(LogLevel.Info))
                log.Write(LogLevel.Info, template, LogTemplate.Format(template, a, b, c), null);
        }

        [HideInCallstack]
        public static void Info<T1, T2, T3, T4>(this ILog log, string template, T1 a, T2 b, T3 c, T4 d)
        {
            if (log.IsEnabled(LogLevel.Info))
                log.Write(LogLevel.Info, template, LogTemplate.Format(template, a, b, c, d), null);
        }
    }
}
