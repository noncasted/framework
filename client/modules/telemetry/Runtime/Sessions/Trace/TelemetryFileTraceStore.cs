using System;
using System.IO;
using UnityEngine;

namespace Global.Telemetry
{
    // A file of the persistent data of the player: a native crash of the process leaves it for the next start. A file that cannot be
    // written stops the trace with one line: the trace is a hint, the game goes on.
    public class TelemetryFileTraceStore : ITelemetryTraceStore
    {
        public TelemetryFileTraceStore()
        {
            _path = Path.Combine(Application.persistentDataPath, "telemetry-session.json");
        }

        private readonly string _path;
        private bool _isBroken;

        public string Read()
        {
            return Guarded(() => File.Exists(_path) ? File.ReadAllText(_path) : null);
        }

        public void Write(string text)
        {
            Guarded(() =>
            {
                File.WriteAllText(_path, text);
                return null;
            });
        }

        public void Clear()
        {
            Guarded(() =>
            {
                File.Delete(_path);
                return null;
            });
        }

        // The boundary of the file system.
        private string Guarded(Func<string> action)
        {
            if (_isBroken)
                return null;

            try
            {
                return action();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _isBroken = true;
                TelemetrySelfLog.Line($"The trace of the session is off: {exception.GetType().Name}");
                return null;
            }
        }
    }
}
