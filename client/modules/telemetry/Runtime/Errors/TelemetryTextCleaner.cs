using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Global.Telemetry
{
    // Texts as the contract wants them: cut, so the expressions get a bounded text; scrubbed; cut again, the placeholders
    // are longer than what they replace. A loop of errors gives the same text again and again, it is scrubbed once.
    public class TelemetryTextCleaner
    {
        private const string _tooSlow = "<too slow to scrub>";

        private readonly Dictionary<string, string> _messages = new();
        private readonly Dictionary<string, string> _stacks = new();
        private int _scrubsLeft;

        public void BeginPass()
        {
            _scrubsLeft = TelemetryErrorLimits.MaxScrubsPerPass;
        }

        public bool TryCleanMessage(string raw, out string clean)
        {
            return TryClean(_messages, raw, TelemetryLimits.MaxMessageLength, out clean);
        }

        // The contract has no empty stack: without one the field is absent, the clean text is null.
        public bool TryCleanStack(string raw, out string clean)
        {
            clean = null;
            return string.IsNullOrEmpty(raw) || TryClean(_stacks, raw, TelemetryLimits.MaxStackLength, out clean);
        }

        // False: no budget in this pass for a new text.
        private bool TryClean(Dictionary<string, string> memo, string raw, int limit, out string clean)
        {
            var cut = TelemetryLimits.Cut(raw, limit);

            if (memo.TryGetValue(cut, out clean))
                return true;

            if (_scrubsLeft == 0)
                return false;

            _scrubsLeft--;
            clean = TelemetryLimits.Cut(Scrub(cut), limit);

            if (memo.Count == TelemetryErrorLimits.MaxMemoTexts)
                memo.Clear();

            memo[cut] = clean;
            return true;
        }

        // The reaction to a text the expressions choke on: the record stays, without the text. Never the raw one.
        private static string Scrub(string text)
        {
            try
            {
                return TelemetryScrubber.Scrub(text);
            }
            catch (RegexMatchTimeoutException)
            {
                return _tooSlow;
            }
        }
    }
}
