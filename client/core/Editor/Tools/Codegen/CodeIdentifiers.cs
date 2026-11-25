using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Internal
{
    // One set of rules for every name a generator writes into C# (hierarchy bindings, catalogues, scenes, colors):
    // a name taken from an asset or an object must be an identifier, not a keyword, not the name of the enclosing
    // class (CS0542), not a member the class already has or inherits (CS0102, CS0108), and unique in its class.
    public static class CodeIdentifiers
    {
        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int",
            "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out",
            "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try",
            "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        public static bool IsKeyword(string name)
        {
            return Keywords.Contains(name);
        }

        // A letter or an underscore, then letters, digits and underscores; a keyword is not an identifier.
        public static bool IsIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name) || IsKeyword(name))
                return false;

            if (char.IsLetter(name[0]) == false && name[0] != '_')
                return false;

            return name.All(character => char.IsLetterOrDigit(character) || character == '_');
        }

        // Identifiers separated by dots.
        public static bool IsNamespace(string name)
        {
            return string.IsNullOrEmpty(name) == false && name.Split('.').All(IsIdentifier);
        }

        // The members a class derived from the type sees: a generated member with such a name hides it (CS0108) or,
        // for a member of an interface implemented by the generated code, clashes with it.
        public static IEnumerable<string> InheritedMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Public |
                                       BindingFlags.NonPublic |
                                       BindingFlags.Instance |
                                       BindingFlags.Static |
                                       BindingFlags.FlattenHierarchy;

            return type.GetMembers(flags)
                .Where(IsVisibleToDerived)
                .Select(member => member.Name)
                .Distinct(StringComparer.Ordinal);
        }

        private static bool IsVisibleToDerived(MemberInfo member)
        {
            switch (member)
            {
                case MethodBase method:
                    return method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
                case FieldInfo field:
                    return field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;
                case PropertyInfo property:
                    return property.GetAccessors(true).Any(IsVisibleToDerived);
                case EventInfo info:
                    return IsVisibleToDerived(info.GetAddMethod(true));
                case Type nested:
                    return nested.IsNestedPublic || nested.IsNestedFamily || nested.IsNestedFamORAssem;
                default:
                    return false;
            }
        }
    }
}
