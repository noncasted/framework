using System;
using Shared;

namespace Global.MatchMetrics
{
    // The rule of SharedMatchMetricNames for the writes of the game: a wrong name is a mistake of the code, it throws.
    public static class MatchMetricNames
    {
        public static void RequireName(string name)
        {
            if (SharedMatchMetricNames.IsName(name) == false)
                throw new ArgumentException($"[MatchMetrics] The name '{name}': only [a-z0-9._] up to {SharedMatchMetricsLimits.MaxNameLength}");
        }

        public static void RequireTag(string key, string value)
        {
            RequireKey(key);

            if (SharedMatchMetricNames.IsValue(value) == false)
                throw new ArgumentException($"[MatchMetrics] The value '{value}' of '{key}': only [a-z0-9._-] up to {SharedMatchMetricsLimits.MaxTagLength}");
        }

        public static void RequireNumber(string key, double number)
        {
            RequireKey(key);

            if (double.IsNaN(number) || double.IsInfinity(number))
                throw new ArgumentException($"[MatchMetrics] The field '{key}' is not a finite number: {number}");
        }

        private static void RequireKey(string key)
        {
            if (SharedMatchMetricNames.IsKey(key) == false)
                throw new ArgumentException($"[MatchMetrics] The key '{key}': only [a-z0-9._] up to {SharedMatchMetricsLimits.MaxTagLength}");
        }
    }
}
