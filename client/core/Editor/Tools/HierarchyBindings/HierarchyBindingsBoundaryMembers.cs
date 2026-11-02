using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Internal
{
    // Children with their own bindings: the parent holds references to the bindings and does not go inside.
    internal static class HierarchyBindingsBoundaryMembers
    {
        // The name comes from the object, not the type: from outside it is a child like the others,
        // its own bindings class is just responsible for its insides. A child with several bindings gets a
        // field per class: the object name + the type name without the Bindings suffix (Pair + ABindings =
        // PairA). Such a name is not deduplicated with a digit: a taken one is a generation error.
        public static void AddBoundary(
            HierarchyBindingsNode node,
            HierarchyBindingsChildScan scan,
            IdentifierScope used,
            HashSet<string> siblingNames,
            List<string> errors)
        {
            if (scan.Boundaries.Count == 1)
            {
                var single = HierarchyBindingsComponentFields.MakeField(scan.Boundaries[0], scan.Child.name, used, errors, scan.ChildPath);

                if (single == null)
                    return;

                single.SetComment(scan.ChildPath + " (own bindings)");
                node.Fields.Add(single);
                return;
            }

            foreach (var boundary in scan.Boundaries)
            {
                var propertyName = HierarchyBindingsNaming.ToIdentifier(scan.Child.name) + ToShortTypeName(boundary);

                if (ReserveExact(propertyName, scan.ChildPath, used, siblingNames, errors) == false)
                    continue;

                node.Fields.Add(new HierarchyBindingsField(
                    boundary,
                    propertyName,
                    HierarchyBindingsNaming.ToFieldName(propertyName),
                    HierarchyBindingsMemberNames.ToCodeTypeName(boundary),
                    scan.ChildPath + " (own bindings)"));
            }
        }

        private static string ToShortTypeName(Component boundary)
        {
            var name = boundary.GetType().Name;
            var suffix = HierarchyBindingsGeneratedTypes.TypeNameSuffix;

            return name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal)
                ? name.Substring(0, name.Length - suffix.Length)
                : name;
        }

        // The field name for one of several bindings of a child must not match a taken name nor the name of a
        // sibling that gets its field later: otherwise the sibling would silently become "Name2".
        private static bool ReserveExact(
            string propertyName,
            string hierarchyPath,
            IdentifierScope used,
            HashSet<string> siblingNames,
            List<string> errors)
        {
            if (HierarchyBindingsMemberNames.Validate(propertyName, propertyName, hierarchyPath, used, errors) == false)
                return false;

            var fieldName = HierarchyBindingsNaming.ToFieldName(propertyName);

            if (siblingNames.Contains(propertyName) || used.IsFree(propertyName) == false ||
                used.IsFree(fieldName) == false)
            {
                errors.Add(
                    $"'{hierarchyPath}': field '{propertyName}' for one of its bindings collides with another member. " +
                    "Rename the object or the bindings class.");
                return false;
            }

            used.TryDeclare(propertyName);
            used.TryDeclare(fieldName);
            return true;
        }

        // A row of siblings with their own bindings - an array of references per type. The set of types is the
        // same for every element (the signature checked it), but the order of the components may differ, so an
        // element is found by type, not by position.
        public static void AddBoundaryGroup(
            HierarchyBindingsNode node,
            List<HierarchyBindingsChildScan> scans,
            int start,
            int length,
            string baseName,
            string comment,
            IdentifierScope used,
            HashSet<string> siblingNames,
            List<string> errors)
        {
            var first = scans[start];
            var isSingle = first.Boundaries.Count == 1;

            foreach (var template in first.Boundaries)
            {
                var type = template.GetType();
                var plural = HierarchyBindingsNaming.ToPlural(baseName);
                string propertyName;

                if (isSingle)
                {
                    propertyName = HierarchyBindingsMemberNames.DeclareMember(plural, used);
                }
                else
                {
                    propertyName = plural + ToShortTypeName(template);

                    if (ReserveExact(propertyName, first.ChildPath, used, siblingNames, errors) == false)
                        return;
                }

                var field = new HierarchyBindingsField(
                    template,
                    propertyName,
                    HierarchyBindingsNaming.ToFieldName(propertyName),
                    HierarchyBindingsMemberNames.ToCodeTypeName(template),
                    comment + " (own bindings)");

                for (var index = start; index < start + length; index++)
                {
                    var element = scans[index].Boundaries.FirstOrDefault(candidate => candidate.GetType() == type);

                    if (element == null)
                    {
                        errors.Add($"'{scans[index].ChildPath}' has no {type.Name}, but its group expects it.");
                        return;
                    }

                    field.Targets.Add(element);
                }

                node.Fields.Add(field);
            }
        }
    }
}
