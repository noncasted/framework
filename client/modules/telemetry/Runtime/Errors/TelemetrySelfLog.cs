using System;
using UnityEngine;

namespace Global.Telemetry
{
    // What the module itself logs goes to the log, not to its own queue. By the thread: an error of the game coming from
    // another thread at this moment is still an error of the game.
    public static class TelemetrySelfLog
    {
        [ThreadStatic]
        private static bool _isInside;

        public static bool IsInside => _isInside;

        public static void Exception(Exception exception)
        {
            var wasInside = _isInside;

            _isInside = true;
            Debug.LogException(exception);
            _isInside = wasInside;
        }

        public static void Line(string text)
        {
            var wasInside = _isInside;

            _isInside = true;
            Debug.Log($"[Telemetry] {text}");
            _isInside = wasInside;
        }

        // The top of the handling of errors: whatever breaks here, the game goes on.
        public static void Guard(Action action)
        {
            var wasInside = _isInside;

            _isInside = true;

            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                _isInside = wasInside;
            }
        }
    }
}
