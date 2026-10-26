using System.Text;

namespace Internal
{
    // Property names come from object names in the hierarchy and component types, so they have
    // to be turned into C# identifiers. Collisions with inherited and generated members are checked by the scanner
    // (IdentifierScope): the generated class itself inherits ObjectBindings and the user class inherits it,
    // so a name collision gives CS0108 in someone else's file and is unpleasant to track down.
    public static class HierarchyBindingsNaming
    {
        private const string DigitPrefix = "N";

        public static string ToIdentifier(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var builder = new StringBuilder(raw.Length);
            var startOfWord = true;

            foreach (var character in raw)
            {
                if (char.IsLetterOrDigit(character) == false)
                {
                    startOfWord = true;
                    continue;
                }

                // Uppercase the first letter of each word, keep the rest as is: "Text (TMP)" gives
                // "TextTMP", not "TextTmp".
                builder.Append(startOfWord ? char.ToUpperInvariant(character) : character);
                startOfWord = false;
            }

            if (builder.Length == 0)
                return string.Empty;

            if (char.IsDigit(builder[0]))
                builder.Insert(0, DigitPrefix);

            return builder.ToString();
        }

        // Leading abbreviations are lowercased entirely: UIElementPointerHandler gives
        // _uiElementPointerHandler, not _uIElementPointerHandler.
        public static string ToFieldName(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return string.Empty;

            var run = 0;

            while (run < propertyName.Length && char.IsUpper(propertyName[run]))
                run++;

            // The last capital of a run starts the next word if a lowercase letter follows it.
            if (run > 1 && run < propertyName.Length)
                run--;

            if (run == 0)
                run = 1;

            var builder = new StringBuilder(propertyName.Length + 1);
            builder.Append('_');

            for (var index = 0; index < propertyName.Length; index++)
                builder.Append(index < run ? char.ToLowerInvariant(propertyName[index]) : propertyName[index]);

            return builder.ToString();
        }

        // Unity numbers siblings with a suffix: "Entry", "Entry_1", "Entry (2)". A group needs
        // a common name, so the number is cut off together with the separator and brackets. A name without a number
        // stays as is, so the first sibling and the rest give the same base.
        public static string StripIndexSuffix(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            var end = raw.Length;

            while (end > 0 && (raw[end - 1] == ')' || char.IsWhiteSpace(raw[end - 1])))
                end--;

            var digitsEnd = end;

            while (end > 0 && char.IsDigit(raw[end - 1]))
                end--;

            // There were no digits - this is not a number but a plain name.
            if (end == digitsEnd)
                return raw;

            while (end > 0 && (raw[end - 1] == '(' || raw[end - 1] == '_' || raw[end - 1] == '-' ||
                               char.IsWhiteSpace(raw[end - 1])))
                end--;

            // The whole name was a number - nothing to cut.
            return end == 0 ? raw : raw.Substring(0, end);
        }

        // The array name is plural, otherwise Entries and Entry in one class could not be told apart.
        public static string ToPlural(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var last = char.ToLowerInvariant(name[name.Length - 1]);
            var previous = name.Length > 1 ? char.ToLowerInvariant(name[name.Length - 2]) : '\0';

            if (last == 'y' && IsVowel(previous) == false)
                return name.Substring(0, name.Length - 1) + "ies";

            if (last == 's' || last == 'x' || last == 'z' ||
                (previous == 'c' && last == 'h') || (previous == 's' && last == 'h'))
                return name + "es";

            return name + "s";
        }

        private static bool IsVowel(char character)
        {
            return character == 'a' || character == 'e' || character == 'i' ||
                   character == 'o' || character == 'u';
        }
    }
}