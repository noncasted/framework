using System;

namespace Global.MatchMetrics
{
    // Names, tags and texts of fields are the only text a stream carries, so no free text gets through them: names and
    // keys [a-z0-9._], values and texts [a-z0-9._-], of a limited length. A wrong one is a mistake of the code: it throws.
    public static class MatchMetricNames
    {
        public static bool IsValid(string text, int maxLength, bool allowDash)
        {
            if (string.IsNullOrEmpty(text) || text.Length > maxLength)
                return false;

            for (var i = 0; i < text.Length; i++)
            {
                var symbol = text[i];

                var isValid = symbol >= 'a' && symbol <= 'z'
                              || symbol >= '0' && symbol <= '9'
                              || symbol == '.'
                              || symbol == '_'
                              || symbol == '-' && allowDash;

                if (isValid == false)
                    return false;
            }

            return true;
        }

        public static void RequireName(string name)
        {
            if (IsValid(name, MatchMetricsLimits.MaxNameLength, false) == false)
                throw new ArgumentException($"[MatchMetrics] The name '{name}': only [a-z0-9._] up to {MatchMetricsLimits.MaxNameLength}");
        }

        public static void RequireTag(string key, string value)
        {
            if (IsValid(key, MatchMetricsLimits.MaxTagLength, false) == false)
                throw new ArgumentException($"[MatchMetrics] The key '{key}': only [a-z0-9._] up to {MatchMetricsLimits.MaxTagLength}");

            if (IsValid(value, MatchMetricsLimits.MaxTagLength, true) == false)
                throw new ArgumentException($"[MatchMetrics] The value '{value}' of '{key}': only [a-z0-9._-] up to {MatchMetricsLimits.MaxTagLength}");
        }

        public static void RequireNumber(string key, double number)
        {
            if (IsValid(key, MatchMetricsLimits.MaxTagLength, false) == false)
                throw new ArgumentException($"[MatchMetrics] The key '{key}': only [a-z0-9._] up to {MatchMetricsLimits.MaxTagLength}");

            if (double.IsNaN(number) || double.IsInfinity(number))
                throw new ArgumentException($"[MatchMetrics] The field '{key}' is not a finite number: {number}");
        }
    }
}
