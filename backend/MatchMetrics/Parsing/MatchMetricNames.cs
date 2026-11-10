namespace Game.MatchMetrics;

// Names, tags and texts of fields are the only text a stream carries, so no free text gets through them: names and keys
// [a-z0-9._], values and texts [a-z0-9._-], of a limited length. The rule of the client module.
public static class MatchMetricNames
{
    public static bool IsName(string? text)
    {
        return IsValid(text, MatchMetricsLimits.MaxNameLength, false);
    }

    public static bool IsKey(string? text)
    {
        return IsValid(text, MatchMetricsLimits.MaxTagLength, false);
    }

    public static bool IsValue(string? text)
    {
        return IsValid(text, MatchMetricsLimits.MaxTagLength, true);
    }

    private static bool IsValid(string? text, int maxLength, bool allowDash)
    {
        if (string.IsNullOrEmpty(text) || text.Length > maxLength)
            return false;

        foreach (var symbol in text)
        {
            var isValid = symbol is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' || symbol == '-' && allowDash;

            if (isValid == false)
                return false;
        }

        return true;
    }
}
