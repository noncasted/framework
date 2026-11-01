using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class TypeNames {
        public static string ForCode(ITypeSymbol type) {
            return Display(type, true);
        }

        public static string ForMetadata(ITypeSymbol type) {
            return Display(type, false);
        }

        private static string Display(ITypeSymbol type, bool fullyQualified) {
            if (type == null)
                return "";

            ITypeSymbol display = type;
            try {
                display = type.WithNullableAnnotation(NullableAnnotation.None);
            }
            catch (System.NotSupportedException) {
                display = type;
            }

            var text = display.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fullyQualified)
                return text;
            return text.Replace("global::", "");
        }

        public static string SafeHint(string fullTypeName) {
            return fullTypeName
                   .Replace("global::", "")
                   .Replace(".", "_")
                   .Replace("<", "_")
                   .Replace(">", "_")
                   .Replace(",", "_")
                   .Replace(" ", "")
                   .Replace("+", "_");
        }
    }
}
