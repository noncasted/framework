using System;

namespace Global.Telemetry
{
    // Names and units are the only text a metric or a mark carries, so free text must not get through them: only
    // [a-z0-9._] of a limited length, the same rule the server applies.
    //
    // A wrong name is a mistake of the programmer, not of the player. In the editor and a development build (strict) it
    // throws at the call, where it gets fixed. In a release the telemetry must not disturb the game: the value is
    // dropped without a record, the name takes no index and the rest of the sample goes on.
    public class TelemetryNameGuard
    {
        public TelemetryNameGuard(bool strict)
        {
            _strict = strict;
        }

        private readonly bool _strict;

        public static bool IsValid(string name, int maxLength)
        {
            if (string.IsNullOrEmpty(name) || name.Length > maxLength)
                return false;

            for (var i = 0; i < name.Length; i++)
            {
                var symbol = name[i];

                var isValid = symbol >= 'a' && symbol <= 'z'
                              || symbol >= '0' && symbol <= '9'
                              || symbol == '.'
                              || symbol == '_';

                if (isValid == false)
                    return false;
            }

            return true;
        }

        public bool AcceptName(string name)
        {
            return IsValid(name, TelemetryLimits.MaxNameLength)
                   || Reject($"The name '{name}': only [a-z0-9._] up to {TelemetryLimits.MaxNameLength} characters");
        }

        // No unit is fine.
        public bool AcceptUnit(string unit)
        {
            return unit == null
                   || IsValid(unit, TelemetryLimits.MaxUnitLength)
                   || Reject($"The unit '{unit}': only [a-z0-9._] up to {TelemetryLimits.MaxUnitLength} characters");
        }

        // False in a release: the caller drops what it was doing.
        public bool Reject(string reason)
        {
            if (_strict)
                throw new ArgumentException($"[Telemetry] {reason}");

            return false;
        }
    }
}
