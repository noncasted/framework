using System;
using UnityEngine;

namespace Internal
{
    // A record into the console of Unity: "[ShortName] message". In a player the levels below Warning go without the stack
    // (its capture is the price of a log, worst on WebGL); in the editor they keep it, the console opens the source.
    public static class UnityLogOutput
    {
        private const LogOption _belowWarning =
#if UNITY_EDITOR
            LogOption.None;
#else
            LogOption.NoStacktrace;
#endif

        [HideInCallstack]
        public static void Write(LogCategory category, LogLevel level, string message, Exception exception)
        {
            if (message == null)
            {
                Debug.LogException(exception);
                return;
            }

            var line = "[" + category.ShortName + "] " + message;

            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.LogFormat(LogType.Log, _belowWarning, null, "{0}", line);
                    break;
                case LogLevel.Warning:
                    Debug.LogFormat(LogType.Warning, LogOption.None, null, "{0}", line);
                    break;
                case LogLevel.Error:
                    Debug.LogFormat(LogType.Error, LogOption.None, null, "{0}", line);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, "[Log] Not a level of a record");
            }

            if (exception != null)
                Debug.LogException(exception);
        }
    }
}
