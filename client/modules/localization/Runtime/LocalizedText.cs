using System;
using System.Globalization;
using Unity.Scripting.LifecycleManagement;

namespace Global.Localization
{
    // A text shown on the screen: a key of the table (with format arguments) or a raw string that is not translated
    // (a player name, a room code, a number). Resolved to the current language when shown, so a label that keeps it
    // (LocalizedLabel) follows a language change. Numbers are formatted with the invariant culture in every language
    // ("1.5", "35%"): the formats are written in the texts ({0:0.##}, {1:0%}).
    [NoAutoStaticsCleanup]
    public readonly struct LocalizedText : IEquatable<LocalizedText>
    {
        public LocalizedText(TextKey key, object[] args)
        {
            _key = key;
            _args = args;
            _raw = null;
        }

        private LocalizedText(string raw)
        {
            _key = default;
            _args = null;
            _raw = raw ?? throw new ArgumentNullException(nameof(raw));
        }

        private readonly TextKey _key;
        private readonly object[] _args;
        private readonly string _raw;

        public static LocalizedText Empty { get; } = new(string.Empty);

        public bool IsEmpty => _raw != null && _raw.Length == 0;

        public static LocalizedText Raw(string text)
        {
            return new LocalizedText(text);
        }

        public static implicit operator LocalizedText(TextKey key)
        {
            return new LocalizedText(key, null);
        }

        public string Resolve()
        {
            if (_raw != null)
                return _raw;

            if (_key.Id == null)
                throw new InvalidOperationException("Default LocalizedText: neither a key nor a raw text");

            var text = Localizer.Get(_key);

            if (_args == null || _args.Length == 0)
                return text;

            var args = new object[_args.Length];

            for (var i = 0; i < _args.Length; i++)
            {
                args[i] = _args[i] switch
                {
                    TextKey key => Localizer.Get(key),
                    LocalizedText inner => inner.Resolve(),
                    _ => _args[i]
                };
            }

            return string.Format(CultureInfo.InvariantCulture, text, args);
        }

        public bool Equals(LocalizedText other)
        {
            return _raw == other._raw && _key.Equals(other._key) && _args == other._args;
        }

        public override bool Equals(object obj)
        {
            return obj is LocalizedText other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_raw, _key, _args);
        }

        public override string ToString()
        {
            return _raw ?? _key.Id;
        }
    }
}
