using System;
using UnityEngine;

namespace Internal
{
    // The levels of the categories under a prefix: a namespace or a type, matched by whole segments ("Global.Network" takes
    // "Global.Network" and "Global.Network.Peer", not "Global.NetworkTools"). The longest matching prefix wins.
    [Serializable]
    public class LogRule
    {
        public LogRule()
        {
        }

        public LogRule(string prefix, LogLevel development, LogLevel release, LogLevel mark = LogLevel.None)
        {
            _prefix = prefix;
            _development = development;
            _release = release;
            _mark = mark;
        }

        [SerializeField] private string _prefix = string.Empty;
        [SerializeField] private LogLevel _development = LogLevel.Info;
        [SerializeField] private LogLevel _release = LogLevel.Warning;

        // From this level on a record also goes to the open session of the client telemetry as a mark.
        [SerializeField] private LogLevel _mark = LogLevel.None;

        public string Prefix => _prefix;
        public LogLevel Development => _development;
        public LogLevel Release => _release;
        public LogLevel Mark => _mark;

        public bool Matches(string category)
        {
            if (category.StartsWith(_prefix, StringComparison.Ordinal) == false)
                return false;

            return category.Length == _prefix.Length || category[_prefix.Length] == '.';
        }
    }
}
