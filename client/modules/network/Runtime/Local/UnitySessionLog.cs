using System;
using Game.Session;

namespace Internal
{
    // The log of a session core inside the client: the messages come formatted, so each is the argument of a template.
    public class UnitySessionLog : ISessionLog
    {
        public UnitySessionLog(ILog log)
        {
            _log = log;
        }

        private readonly ILog _log;

        public void Debug(string message)
        {
            _log.Debug("{Message}", message);
        }

        public void Info(string message)
        {
            _log.Info("{Message}", message);
        }

        public void Warning(string message)
        {
            _log.Warning("{Message}", message);
        }

        public void Error(string message, Exception exception = null)
        {
            if (exception != null)
                _log.Error(exception, "{Message}", message);
            else
                _log.Error("{Message}", message);
        }
    }
}
