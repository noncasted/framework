using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // The names of the members of a generated class: their checks, deduplication and the code names of their types.
    internal static class HierarchyBindingsMemberNames
    {
        public static bool Validate(
            string propertyName,
            string rawName,
            string hierarchyPath,
            IdentifierScope used,
            List<string> errors)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                errors.Add($"'{hierarchyPath}': name '{rawName}' is not a C# identifier.");
                return false;
            }

            if (used.IsReserved(propertyName))
            {
                errors.Add(
                    $"'{hierarchyPath}': '{propertyName}' collides with a member the generated class inherits or " +
                    "declares itself. Rename the object.");
                return false;
            }

            return true;
        }

        // Duplicate names within one class are disambiguated with a suffix. The traversal order is deterministic,
        // so the suffix is stable between runs as long as the hierarchy has not changed. The backing field is
        // derived from the property and must be free too: HPBar and HpBar both give _hpBar.
        public static string DeclareMember(string name, IdentifierScope used)
        {
            for (var index = 1; index < 1000; index++)
            {
                var candidate = index == 1 ? name : name + index;
                var fieldName = HierarchyBindingsNaming.ToFieldName(candidate);

                if (used.IsFree(candidate) == false || used.IsFree(fieldName) == false)
                    continue;

                used.TryDeclare(candidate);
                used.TryDeclare(fieldName);
                return candidate;
            }

            throw new InvalidOperationException($"No free name for '{name}' in {used.EnclosingTypeName}.");
        }

        // The class of a child is a member of the parent and the enclosing type of the child's own members, so its
        // name must be free on both sides (CS0102 in the parent, CS0542 inside).
        public static string DeclareType(string name, IdentifierScope parent, IdentifierScope own)
        {
            for (var index = 1; index < 1000; index++)
            {
                var candidate = index == 1 ? name : name + index;

                if (parent.IsFree(candidate) == false || own.IsDeclared(candidate))
                    continue;

                parent.TryDeclare(candidate);
                own.SetEnclosingTypeName(candidate);
                return candidate;
            }

            throw new InvalidOperationException($"No free class name for '{name}' in {parent.EnclosingTypeName}.");
        }

        // Component types are read from the script sources while the last compile failed (see ScriptTypeNames).
        public static string ToCodeTypeName(Component component)
        {
            var fullName = ScriptTypeNames.FullName(component.GetType(), ScriptTypeNames.ScriptOf(component));
            return "global::" + fullName.Replace('+', '.');
        }
    }
}
