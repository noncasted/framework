using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // The levels of the logs, a field of the OptionsContainer of the game: one set for the development builds and the editor
    // (Debug.isDebugBuild), one for the release ones. Debug is cut out of a release build by the compiler anyway
    // (LogExtensions), so its level there only matters with FRAMEWORK_LOG_DEBUG.
    [Serializable]
    public class LogOptions
    {
        public LogOptions()
        {
        }

        public LogOptions(LogLevel development, LogLevel release, LogLevel mark, params LogRule[] rules)
        {
            _development = development;
            _release = release;
            _mark = mark;
            _rules = rules;
        }

        [SerializeField] private LogLevel _development = LogLevel.Info;
        [SerializeField] private LogLevel _release = LogLevel.Warning;
        [SerializeField] private LogLevel _mark = LogLevel.None;
        [SerializeField] private LogRule[] _rules = Array.Empty<LogRule>();

        public LogLevel Development => _development;
        public LogLevel Release => _release;
        public LogLevel Mark => _mark;
        public IReadOnlyList<LogRule> Rules => _rules;

        // The levels of a category in a build of this kind.
        public (LogLevel Shown, LogLevel Mark) LevelsOf(string category, bool isDevelopment)
        {
            LogRule best = null;

            foreach (var rule in _rules)
            {
                if (rule.Matches(category) == false)
                    continue;

                if (best == null || rule.Prefix.Length > best.Prefix.Length)
                    best = rule;
            }

            if (best == null)
                return (isDevelopment ? _development : _release, _mark);

            return (isDevelopment ? best.Development : best.Release, best.Mark);
        }
    }
}
