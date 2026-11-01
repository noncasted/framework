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

    // The names of one generated class. Reserved names are the members the generator writes itself or the class
    // inherits; declared ones are the members taken so far. The enclosing type name may be set after the members:
    // the bindings of a child are named by its parent once its contents are known.
    public sealed class IdentifierScope
    {
        public IdentifierScope(string enclosingTypeName, IEnumerable<string> reserved)
        {
            EnclosingTypeName = enclosingTypeName;
            _reserved = new HashSet<string>(reserved, StringComparer.Ordinal);
        }

        private readonly HashSet<string> _reserved;
        private readonly HashSet<string> _declared = new(StringComparer.Ordinal);

        public string EnclosingTypeName { get; private set; }

        public void SetEnclosingTypeName(string name)
        {
            EnclosingTypeName = name;
        }

        // A keyword or a member the class has anyway: renaming with a digit would hide the problem, the source
        // has to be renamed.
        public bool IsReserved(string name)
        {
            return CodeIdentifiers.IsKeyword(name) || _reserved.Contains(name);
        }

        public bool IsDeclared(string name)
        {
            return _declared.Contains(name);
        }

        // Null when the name can be declared, otherwise why it cannot.
        public string FindConflict(string name)
        {
            if (CodeIdentifiers.IsKeyword(name))
                return "is a C# keyword";

            if (CodeIdentifiers.IsIdentifier(name) == false)
                return "is not a C# identifier";

            if (string.Equals(name, EnclosingTypeName, StringComparison.Ordinal))
                return $"is the name of the enclosing class {EnclosingTypeName}";

            if (_reserved.Contains(name))
                return $"is a member {EnclosingTypeName} already has";

            if (_declared.Contains(name))
                return $"is declared in {EnclosingTypeName} twice";

            return null;
        }

        public bool IsFree(string name)
        {
            return FindConflict(name) == null;
        }

        public bool TryDeclare(string name)
        {
            if (IsFree(name) == false)
                return false;

            _declared.Add(name);
            return true;
        }

        public void Declare(string name, string source)
        {
            var conflict = FindConflict(name);

            if (conflict != null)
                throw new InvalidOperationException($"{source}: the name '{name}' {conflict}. Rename the source.");

            _declared.Add(name);
        }
    }
}
