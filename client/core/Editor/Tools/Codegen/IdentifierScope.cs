using System;
using System.Collections.Generic;

namespace Internal
{
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
