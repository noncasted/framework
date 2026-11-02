using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class ScopeNames {
        private static readonly HashSet<string> Keywords = new HashSet<string> {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while"
        };

        // The namespace comes from the symbol: in the id (the type with dots, as the runtime builds it) a namespace and a
        // type around the root look the same, and a class generated in a namespace named after a type does not compile.
        public static void ParseRoot(
            IMethodSymbol root,
            string rootId,
            out string ns,
            out string typeName,
            out string methodName,
            out string className) {
            var type = root.ContainingType;
            ns = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
            typeName = NestedTypeName(type);

            var plus = rootId.IndexOf('+');
            var head = plus >= 0 ? rootId.Substring(0, plus) : rootId;
            methodName = head.Substring(head.LastIndexOf('.') + 1);
            className = typeName + methodName + "Container";
        }

        // With the types around it: roots of one type name in two outer types are in one namespace now.
        private static string NestedTypeName(INamedTypeSymbol type) {
            return type.ContainingType == null ? type.Name : NestedTypeName(type.ContainingType) + "_" + type.Name;
        }

        public static string Short(string fullyQualified) {
            if (string.IsNullOrEmpty(fullyQualified))
                return "Service";

            var value = StripGlobal(fullyQualified);
            var tick = value.IndexOf('<');
            if (tick >= 0)
                value = value.Substring(0, tick);

            var last = value.LastIndexOf('.');
            if (last >= 0)
                value = value.Substring(last + 1);

            var plus = value.LastIndexOf('+');
            if (plus >= 0)
                value = value.Substring(plus + 1);

            return string.IsNullOrEmpty(value) ? "Service" : value;
        }

        public static string Field(string fullyQualified, HashSet<string> used) {
            return Unique("_" + Camel(Short(fullyQualified)), used);
        }

        public static string MarkerField(string fullyQualified, HashSet<string> used) {
            var shortName = Short(fullyQualified);
            if (shortName.Length > 1 && shortName[0] == 'I' && char.IsUpper(shortName[1]))
                shortName = shortName.Substring(1);

            return Unique("_" + Camel(shortName), used);
        }

        public static string Parameter(string preferred, string fullyQualified, HashSet<string> used) {
            var name = preferred;
            if (IsSimpleIdentifier(name) == false)
                name = Camel(Short(fullyQualified));
            if (string.IsNullOrEmpty(name))
                name = "value";
            name = Escape(name);
            return Unique(name, used);
        }

        public static string CreateMethod(string fullyQualified, HashSet<string> used) {
            return Unique("Create" + Short(fullyQualified), used);
        }

        public static string Camel(string name) {
            if (string.IsNullOrEmpty(name))
                return "value";
            if (name.Length == 1)
                return name.ToLowerInvariant();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        public static string Escape(string name) {
            if (Keywords.Contains(name))
                return "@" + name;
            return name;
        }

        public static string Unique(string name, HashSet<string> used) {
            var candidate = name;
            var index = 2;
            while (used.Contains(candidate)) {
                candidate = name + index.ToString();
                index++;
            }

            used.Add(candidate);
            return candidate;
        }

        public static bool IsSimpleIdentifier(string value) {
            if (string.IsNullOrEmpty(value))
                return false;
            if (char.IsLetter(value[0]) == false && value[0] != '_')
                return false;
            for (var i = 1; i < value.Length; i++) {
                if (char.IsLetterOrDigit(value[i]) == false && value[i] != '_')
                    return false;
            }

            return true;
        }

        public static string StripGlobal(string value) {
            const string prefix = "global::";
            if (value != null && value.StartsWith(prefix, System.StringComparison.Ordinal))
                return value.Substring(prefix.Length);
            return value ?? "";
        }
    }
}
