using System;

namespace Global.Localization
{
    // The key of a text in the table (LocalizationOptions.Table). Keys are declared in code (the static *Texts classes
    // of the game modules), never typed in the editor; a test of the game checks that every declared key is in the
    // table (LocalizationTable.Contains, Keys).
    public readonly struct TextKey : IEquatable<TextKey>
    {
        public TextKey(string id)
        {
            if (string.IsNullOrEmpty(id) == true)
                throw new ArgumentException("Empty text key", nameof(id));

            Id = id;
        }

        public string Id { get; }

        // The text with string.Format arguments; an argument that is a TextKey or a LocalizedText is translated too.
        public LocalizedText Format(params object[] args)
        {
            return new LocalizedText(this, args);
        }

        public bool Equals(TextKey other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is TextKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id == null ? 0 : Id.GetHashCode();
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
