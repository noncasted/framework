using System;
using Game.Session;

namespace Internal
{
    // The log of a session core inside the client.
    public class UnitySessionLog : ISessionLog
    {
        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
            UnityEngine.Debug.Log($"[Network] [Local] {message}");
        }

        public void Warning(string message)
        {
            UnityEngine.Debug.LogWarning($"[Network] [Local] {message}");
        }

        public void Error(string message, Exception exception = null)
        {
            UnityEngine.Debug.LogError($"[Network] [Local] {message}");

            if (exception != null)
                UnityEngine.Debug.LogException(exception);
        }
    }
}
