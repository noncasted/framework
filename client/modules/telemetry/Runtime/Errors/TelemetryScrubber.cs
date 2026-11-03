using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Global.Telemetry
{
    // The first of the two cleanings of the text of an error, the same rules as the Scrubber of the server
    // (backend/ClientTelemetry/Privacy/Scrubber.cs, change them together) and the same corpus. What is left: the type of the
    // exception, the template of the message, the names of methods. A name written as a plain word cannot be told from the
    // template: see CONTRACT.md.
    //
    // Unity has no NonBacktracking, so the price of a bad text is paid here: unbroken runs are replaced before the
    // expressions, they are what makes them slow, and a text that still is too slow ends in the timeout.
    public static class TelemetryScrubber
    {
        private const RegexOptions _options = RegexOptions.CultureInvariant;
        private const RegexOptions _ignoreCase = _options | RegexOptions.IgnoreCase;
        private const int _maxRunLength = 160;
        private const int _maxPlainWordLength = 32;
        private const int _maxUpperWordWithDigitsLength = 12;
        private const int _maxWordWithDigitsLength = 24;
        private const int _minHexWordLength = 8;
        private const int _maxShortDigitRun = 2;

        private const string _secretKeys = @"token|password|passwd|pwd|secret|api[_\-]?key|authorization|cookie|credentials?|init_?data|tgwebappdata";
        private const string _personalKeys = @"name|user|username|login|e?mail|phone|nick|nickname|player|account|address";
        private const string _domainZones = "com|net|org|ru|su|ua|by|kz|de|uk|fr|cn|jp|br|io|me|app|dev|xyz|gg|co|info|tv|site|online|lan|local|internal|example";
        private const string _fileExtensions = "txt|json|dat|png|jpg|jpeg|pdf|csv|xml|cfg|ini|sqlite|bak|zip|js|wasm|cs";

        private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(100);

        private static readonly Regex _url = Rule(@"[a-zA-Z][a-zA-Z0-9+.\-]*://[^\s?#]*(?:[?#][^\r\n]*)?");
        private static readonly Regex _urlWithoutHost = Rule(@"\b(?:data|blob|javascript|about|mailto|tel|sms|tg):[^\s]*");
        private static readonly Regex _jwt = Rule(@"\beyJ[A-Za-z0-9_\-]+(?:\.[A-Za-z0-9_\-]+)*");
        private static readonly Regex _authScheme = Rule(@"\b(bearer|basic)\s+[A-Za-z0-9+/=._\-]{4,}", _ignoreCase);
        private static readonly Regex _secretValue = Rule(@"([\w.\-]*(?:" + _secretKeys + @"))[\\""']*\s*[:=]\s*([^\r\n]*)", _ignoreCase);
        private static readonly Regex _personalValue = Rule(@"([\w.\-]*(?:" + _personalKeys + @"))[\\""']*\s*[:=]\s*([^\r\n]*)", _ignoreCase);
        private static readonly Regex _queryAfterDelimiter = Rule(@"[?#&][\w.\-%]+=[^\r\n]*");
        private static readonly Regex _queryOfPairs = Rule(@"[\w.\-%]+=[^&\s]*&[^\r\n]*");
        private static readonly Regex _pathWithQuery = Rule(@"\S*[/\\]\S*[?#][^\r\n]*");
        private static readonly Regex _email = Rule(@"[^\s<>()\[\],;:""']*@\S*");
        private static readonly Regex _guid = Rule(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        private static readonly Regex _ipV4 = Rule(@"\b[0-9]{1,3}(?:\.[0-9]{1,3}){3}\b");
        private static readonly Regex _ipV6 = Rule(@"(^|[^0-9A-Za-z_:.<])((?:[0-9a-fA-F]{1,4})?(?::[0-9a-fA-F]{0,4}){2,7}(?:%\w+)?)", _options | RegexOptions.Multiline);
        private static readonly Regex _percentEncoded = Rule(@"\S*%[0-9a-fA-F]{2}\S*");
        private static readonly Regex _base64 = Rule(@"[A-Za-z0-9+/]{8,}={1,2}(\s|[,;)\]}]|$)", _options | RegexOptions.Multiline);
        private static readonly Regex _doubleQuoted = Rule(@"""[^""\r\n]*""?");
        private static readonly Regex _singleQuoted = Rule(@"(^|[^\w])'[^'\r\n]*'?", _options | RegexOptions.Multiline);
        private static readonly Regex _backticked = Rule(@"(^|[^\w])`[^`\r\n]*`", _options | RegexOptions.Multiline);
        private static readonly Regex _braced = Rule(@"\{[^{}\r\n]*\}");
        private static readonly Regex _angledWithSpace = Rule(@"<[^<>\r\n]*\s[^<>\r\n]*>");
        private static readonly Regex _typographicQuoted = Rule(@"[«“‘„][^»”’\r\n]*[»”’]?");

        // Everything out of printable ASCII: a text of a player or a localized one, and the NUL that Postgres cannot store.
        private static readonly Regex _notPrintableAscii = Rule(@"[^\x09\x0A\x0D\x20-\x7E]+");
        private static readonly Regex _absolutePath = Rule(@"(^|[\s(\[=,:])(?:[A-Za-z]:[\\/]|[\\/]{1,2}[^\s\\/]|~[\\/])[^\r\n]*", _options | RegexOptions.Multiline);
        private static readonly Regex _relativePath = Rule(@"[^\s()\[\],;""']*[/\\][^\s()\[\],;""']*");

        // Outer/Inner:Method: a nested type in a Unity stack, the only thing with a slash that is kept.
        private static readonly Regex _nestedTypeFrame = Rule(@"\A[\w.<>`$+\-]+(?:/[\w.<>`$+\-]+)+:[A-Za-z_<.][\w.<>`$]*\z");
        private static readonly Regex _keyValue = Rule(@"([\w.\-]+)\s*=\s*[^=>\s,;&)\]}][^\s,;&)\]}]*");
        private static readonly Regex _hostWithPort = Rule(@"\b[a-zA-Z0-9\-]+(?:\.[a-zA-Z0-9\-]+)+:[0-9]+");

        // System.Net and System.IO are names of code: a domain is all lower or all upper case.
        private static readonly Regex _domain = Rule(@"\b[a-z0-9\-]+(?:\.[a-z0-9\-]+)*\.(?:" + _domainZones + @")\b");
        private static readonly Regex _upperDomain = Rule(@"\b[A-Z0-9\-]+(?:\.[A-Z0-9\-]+)*\.(?:" + _domainZones.ToUpperInvariant() + @")\b");
        private static readonly Regex _wwwHost = Rule(@"\bwww\.\S*", _ignoreCase);
        private static readonly Regex _fileName = Rule(@"[\w\-.]+\.(?:" + _fileExtensions + @")\b");

        // A backtick with digits is the arity of a generic type (List`1), kept as a part of the name of a method.
        private static readonly Regex _word = Rule(@"`[0-9]{1,2}\b|[A-Za-z0-9_]+");

        // After a key: the frame of a method, CancellationToken:ThrowIfCancellationRequested (), not a value.
        private static readonly Regex _frameAfterKey = Rule(@"\A\w+\s*\(");

        // The text must be cut to the limits of the contract before this: the time grows much faster than the length.
        // Throws RegexMatchTimeoutException on a text too slow to scrub.
        public static string Scrub(string text)
        {
            text = ReplaceLongRuns(text);
            text = _url.Replace(text, "<url>");
            text = _urlWithoutHost.Replace(text, "<url>");
            text = _jwt.Replace(text, "<secret>");
            text = _authScheme.Replace(text, "$1 <secret>");
            text = _secretValue.Replace(text, ScrubSecretValue);
            text = _pathWithQuery.Replace(text, "<url>");
            text = _queryAfterDelimiter.Replace(text, "<url>");
            text = _queryOfPairs.Replace(text, "<url>");
            text = _personalValue.Replace(text, ScrubPersonalValue);
            text = _email.Replace(text, "<email>");
            text = _guid.Replace(text, "<guid>");
            text = _ipV4.Replace(text, "<ip>");
            text = _ipV6.Replace(text, ScrubIpV6);
            text = _percentEncoded.Replace(text, "<text>");
            text = _base64.Replace(text, "<id>$1");
            text = _doubleQuoted.Replace(text, "\"<str>\"");
            text = _singleQuoted.Replace(text, "$1'<str>'");
            text = _backticked.Replace(text, "$1`<str>`");
            text = _braced.Replace(text, "{<str>}");
            text = _typographicQuoted.Replace(text, "<str>");
            text = _notPrintableAscii.Replace(text, "<text>");
            text = _angledWithSpace.Replace(text, "<str>");
            text = _wwwHost.Replace(text, "<url>");
            text = _absolutePath.Replace(text, "$1<path>");
            text = _relativePath.Replace(text, ScrubRelativePath);
            text = _keyValue.Replace(text, "$1=<v>");
            text = _hostWithPort.Replace(text, "<url>");
            text = _domain.Replace(text, "<url>");
            text = _upperDomain.Replace(text, "<url>");
            text = _fileName.Replace(text, "<file>");
            text = _word.Replace(text, ScrubWord);
            return text;
        }

        private static Regex Rule(string pattern, RegexOptions options = _options)
        {
            return new Regex(pattern, options, _timeout);
        }

        private static string ReplaceLongRuns(string text)
        {
            StringBuilder result = null;
            var copied = 0;
            var runStart = 0;

            for (var i = 0; i <= text.Length; i++)
            {
                if (i < text.Length && char.IsWhiteSpace(text[i]) == false)
                    continue;

                if (i - runStart > _maxRunLength)
                {
                    result ??= new StringBuilder(text.Length);
                    result.Append(text, copied, runStart - copied).Append("<long>");
                    copied = i;
                }

                runStart = i + 1;
            }

            return result == null ? text : result.Append(text, copied, text.Length - copied).ToString();
        }

        private static string ScrubSecretValue(Match match)
        {
            return _frameAfterKey.IsMatch(match.Groups[2].Value) ? match.Value : match.Groups[1].Value + "=<secret>";
        }

        private static string ScrubPersonalValue(Match match)
        {
            return _frameAfterKey.IsMatch(match.Groups[2].Value) ? match.Value : match.Groups[1].Value + "=<text>";
        }

        private static string ScrubIpV6(Match match)
        {
            return HasDigit(match.Groups[2].Value) ? match.Groups[1].Value + "<ip>" : match.Value;
        }

        private static string ScrubRelativePath(Match match)
        {
            return _nestedTypeFrame.IsMatch(match.Value) ? match.Value : "<path>";
        }

        // Names of code keep their short digits (Vector3, Int32, d__12, DisplayClass12_0), anything else with digits is an id.
        private static string ScrubWord(Match match)
        {
            var word = match.Value;

            if (word[0] == '`')
                return word;

            if (IsAllDigits(word))
                return "<n>";

            if (word.Length >= _minHexWordLength && IsAllHex(word))
                return "<id>";

            if (HasDigit(word) == false)
                return word.Length >= _maxPlainWordLength ? "<id>" : word;

            return IsCodeNameWithDigits(word) ? word : "<id>";
        }

        private static bool IsCodeNameWithDigits(string word)
        {
            if (IsDigit(word[0]) || word.Length >= _maxWordWithDigitsLength)
                return false;

            var hasLower = HasLower(word);

            if (word.Length >= _maxUpperWordWithDigitsLength && hasLower == false)
                return false;

            var digitRuns = 0;
            var runLength = 0;

            foreach (var symbol in word)
            {
                if (IsDigit(symbol) == false)
                {
                    runLength = 0;
                    continue;
                }

                if (runLength == 0)
                    digitRuns++;

                runLength++;

                if (runLength > _maxShortDigitRun)
                    return false;
            }

            // A1B2C3 and x2y4z6 are generated, not named.
            return digitRuns <= 2 && (digitRuns == 1 || hasLower);
        }

        private static bool IsDigit(char symbol)
        {
            return symbol >= '0' && symbol <= '9';
        }

        private static bool HasDigit(string word)
        {
            foreach (var symbol in word)
            {
                if (IsDigit(symbol))
                    return true;
            }

            return false;
        }

        private static bool HasLower(string word)
        {
            foreach (var symbol in word)
            {
                if (symbol >= 'a' && symbol <= 'z')
                    return true;
            }

            return false;
        }

        private static bool IsAllDigits(string word)
        {
            foreach (var symbol in word)
            {
                if (IsDigit(symbol) == false)
                    return false;
            }

            return true;
        }

        private static bool IsAllHex(string word)
        {
            foreach (var symbol in word)
            {
                var isHex = IsDigit(symbol) || (symbol >= 'a' && symbol <= 'f') || (symbol >= 'A' && symbol <= 'F');

                if (isHex == false)
                    return false;
            }

            return true;
        }
    }
}
