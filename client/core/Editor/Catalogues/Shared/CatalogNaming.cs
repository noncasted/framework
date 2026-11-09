using System.Text;

namespace Internal
{
    // Group and property names are built the same way in all catalogs: letters and digits only,
    // with a placeholder prefix if the identifier starts with a digit.
    public static class CatalogNaming
    {
        public static string ToIdentifier(string name, string digitPrefix)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var identifier = Sanitize(name);

            if (identifier.Length == 0)
                return string.Empty;

            if (char.IsDigit(identifier[0]))
                return digitPrefix + identifier;

            return identifier;
        }

        public static string ToGroupName(string name)
        {
            return ToIdentifier(name, "G");
        }

        // Nested group: segments separated by dots, each segment is a separate identifier.
        // "Game/Players" and "Game.Players" both give "Game.Players".
        public static string ToGroupPath(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var builder = new StringBuilder(name.Length);

            foreach (var segment in name.Split('.', '/', '\\'))
            {
                var identifier = ToGroupName(segment);

                if (identifier.Length == 0)
                    continue;

                if (builder.Length > 0)
                    builder.Append('.');

                builder.Append(identifier);
            }

            return builder.ToString();
        }

        // The backing field of a generated property: "Hero" gives "_hero".
        internal static string ToFieldName(string propertyName)
        {
            return "_" + char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
        }

        // Group path "Game.Players" -> "Game_Players": the name of the group, the assets and the class.
        internal static string ToGroupKey(string groupPath)
        {
            return groupPath.Replace('.', '_');
        }

        public static string Sanitize(string name)
        {
            var builder = new StringBuilder(name.Length);

            foreach (var character in name)
            {
                if (char.IsLetterOrDigit(character))
                    builder.Append(character);
            }

            return builder.ToString();
        }
    }
}