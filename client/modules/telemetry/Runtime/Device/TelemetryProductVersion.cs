namespace Global.Telemetry
{
    // A browser or an operating system only down to the major version: "Android OS 14 / API-34 (UP1A...)" gives
    // "Android OS" and 14. Whatever follows may identify a device and is never kept.
    public readonly struct TelemetryProductVersion
    {
        private const int _maxNameLength = 32;
        private const int _maxMajorDigits = 4;

        public TelemetryProductVersion(string name, int? major)
        {
            Name = name;
            Major = major;
        }

        public string Name { get; }
        public int? Major { get; }

        public static TelemetryProductVersion Parse(string text)
        {
            var nameEnd = 0;

            while (nameEnd < text.Length && nameEnd < _maxNameLength && IsNameSymbol(text[nameEnd]))
                nameEnd++;

            var name = text.Substring(0, nameEnd).Trim();

            return new TelemetryProductVersion(name.Length == 0 ? null : name, ParseMajor(text, nameEnd));
        }

        private static bool IsNameSymbol(char symbol)
        {
            return symbol >= 'a' && symbol <= 'z'
                   || symbol >= 'A' && symbol <= 'Z'
                   || symbol == ' ' || symbol == '.' || symbol == '-';
        }

        // The number has to stand right after the name, a later one (a build, a kernel) is not the version.
        private static int? ParseMajor(string text, int start)
        {
            int? major = null;

            for (var i = start; i < text.Length && i < start + _maxMajorDigits; i++)
            {
                var symbol = text[i];

                if (symbol < '0' || symbol > '9')
                    break;

                major = (major ?? 0) * 10 + (symbol - '0');
            }

            return major;
        }
    }
}
