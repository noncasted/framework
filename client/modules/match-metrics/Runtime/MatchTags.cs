using System;
using Shared;

namespace Global.MatchMetrics
{
    // Up to 4 pairs key=value of a series: a key [a-z0-9._] up to 32, a value [a-z0-9._-] up to 32, the keys distinct. A
    // value type: built where the series is declared, MatchTags.Of("source", "orb").With("card", "12").
    public readonly struct MatchTags
    {
        private MatchTags(
            int count,
            string key0, string value0,
            string key1, string value1,
            string key2, string value2,
            string key3, string value3)
        {
            Count = count;
            _key0 = key0;
            _value0 = value0;
            _key1 = key1;
            _value1 = value1;
            _key2 = key2;
            _value2 = value2;
            _key3 = key3;
            _value3 = value3;
        }

        private readonly string _key0;
        private readonly string _value0;
        private readonly string _key1;
        private readonly string _value1;
        private readonly string _key2;
        private readonly string _value2;
        private readonly string _key3;
        private readonly string _value3;

        public int Count { get; }

        public static MatchTags Of(string key, string value)
        {
            return default(MatchTags).With(key, value);
        }

        public MatchTags With(string key, string value)
        {
            MatchMetricNames.RequireTag(key, value);

            for (var i = 0; i < Count; i++)
            {
                if (GetKey(i) == key)
                    throw new ArgumentException($"[MatchMetrics] The tag '{key}' twice");
            }

            return Count switch
            {
                0 => new MatchTags(1, key, value, null, null, null, null, null, null),
                1 => new MatchTags(2, _key0, _value0, key, value, null, null, null, null),
                2 => new MatchTags(3, _key0, _value0, _key1, _value1, key, value, null, null),
                3 => new MatchTags(4, _key0, _value0, _key1, _value1, _key2, _value2, key, value),
                _ => throw new ArgumentException($"[MatchMetrics] Over {SharedMatchMetricsLimits.MaxTags} tags: '{key}'")
            };
        }

        public string GetKey(int index)
        {
            return index switch
            {
                0 when Count > 0 => _key0,
                1 when Count > 1 => _key1,
                2 when Count > 2 => _key2,
                3 when Count > 3 => _key3,
                _ => throw new ArgumentOutOfRangeException(nameof(index), index, "[MatchMetrics] No tag")
            };
        }

        public string GetValue(int index)
        {
            return index switch
            {
                0 when Count > 0 => _value0,
                1 when Count > 1 => _value1,
                2 when Count > 2 => _value2,
                3 when Count > 3 => _value3,
                _ => throw new ArgumentOutOfRangeException(nameof(index), index, "[MatchMetrics] No tag")
            };
        }

        // The identity of a series with its name: the order of the tags does not matter.
        public string ToKey(string name)
        {
            var pairs = new string[Count];

            for (var i = 0; i < Count; i++)
                pairs[i] = GetKey(i) + "=" + GetValue(i);

            Array.Sort(pairs, StringComparer.Ordinal);

            return name + "|" + string.Join(",", pairs);
        }
    }
}
