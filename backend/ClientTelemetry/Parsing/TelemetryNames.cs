namespace Game.ClientTelemetry;

// A metric, a mark and a unit are named in code: the closed alphabet leaves no room for a free text.
public static class TelemetryNames
{
    public static bool IsValidName(string name)
    {
        return IsValid(name, ClientTelemetryLimits.MaxNameLength);
    }

    public static bool IsValidUnit(string unit)
    {
        return IsValid(unit, ClientTelemetryLimits.MaxUnitLength);
    }

    private static bool IsValid(string text, int maxLength)
    {
        if (text.Length == 0 || text.Length > maxLength)
            return false;

        foreach (var symbol in text)
        {
            var allowed = symbol is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_';
            if (!allowed)
                return false;
        }

        return true;
    }
}
