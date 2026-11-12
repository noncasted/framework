namespace Global.MatchMetrics
{
    // A field of an event: a number or a short text of the code (a key [a-z0-9._] up to 32, a text [a-z0-9._-] up to 32),
    // never a free text of a player.
    public readonly struct MatchEventField
    {
        private MatchEventField(string key, double number, string text)
        {
            Key = key;
            Number = number;
            Text = text;
        }

        public string Key { get; }
        public double Number { get; }

        // Null: a number.
        public string Text { get; }

        public static MatchEventField Of(string key, double number)
        {
            MatchMetricNames.RequireNumber(key, number);
            return new MatchEventField(key, number, null);
        }

        public static MatchEventField Of(string key, string text)
        {
            MatchMetricNames.RequireTag(key, text);
            return new MatchEventField(key, 0d, text);
        }
    }
}
