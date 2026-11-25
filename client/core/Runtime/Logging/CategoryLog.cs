using System;
using UnityEngine;

namespace Internal
{
    // A log over its category: what Logs.Of gives.
    public class CategoryLog : ILog
    {
        public CategoryLog(LogCategory category)
        {
            _category = category;
        }

        private readonly LogCategory _category;

        public string Category => _category.Name;

        public bool IsEnabled(LogLevel level)
        {
            return _category.IsEnabled(level);
        }

        [HideInCallstack]
        public void Write(LogLevel level, string template, string message, Exception exception)
        {
            Logs.Write(_category, level, template, message, exception);
        }
    }

    public class CategoryLog<T> : CategoryLog, ILog<T>
    {
        public CategoryLog(LogCategory category) : base(category)
        {
        }
    }
}
