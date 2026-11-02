using System;
using System.Collections.Generic;

namespace Internal
{
    // The scanned children become members of their parent: a row collapses into an array, the rest stay single.
    internal static class HierarchyBindingsChildMembers
    {
        // Only consecutive siblings with a common name and matching contents are collapsed: the array
        // must read the same way as the row in the hierarchy, and the class is shared by all.
        public static int GroupLength(List<HierarchyBindingsChildScan> scans, int start)
        {
            var first = scans[start];

            if (string.IsNullOrEmpty(HierarchyBindingsNaming.ToIdentifier(first.BaseName)))
                return 1;

            var length = 1;

            while (start + length < scans.Count)
            {
                var next = scans[start + length];

                if (string.Equals(next.BaseName, first.BaseName, StringComparison.Ordinal) == false)
                    break;

                if (string.Equals(next.Signature, first.Signature, StringComparison.Ordinal) == false)
                    break;

                length++;
            }

            return length;
        }

        public static bool TryAddGroup(
            HierarchyBindingsNode node,
            List<HierarchyBindingsChildScan> scans,
            int start,
            int length,
            IdentifierScope used,
            HashSet<string> siblingNames,
            List<string> errors)
        {
            var first = scans[start];
            var baseName = HierarchyBindingsNaming.ToIdentifier(first.BaseName);

            if (HierarchyBindingsMemberNames.Validate(baseName, first.BaseName, first.ChildPath, used, errors) == false)
                return false;

            var comment = $"{first.ChildPath} .. {scans[start + length - 1].Child.name} ({length} items)";

            if (first.Boundaries != null)
            {
                HierarchyBindingsBoundaryMembers.AddBoundaryGroup(node, scans, start, length, baseName, comment, used, siblingNames, errors);
                return true;
            }

            var propertyName = HierarchyBindingsMemberNames.DeclareMember(HierarchyBindingsNaming.ToPlural(baseName), used);
            var fieldName = HierarchyBindingsNaming.ToFieldName(propertyName);

            var template = first.Node;
            template.SetNames(HierarchyBindingsMemberNames.DeclareType(baseName + "Bindings", used, template.Members), propertyName, fieldName);

            for (var index = start; index < start + length; index++)
                template.Elements.Add(scans[index].Node);

            node.Children.Add(template);
            return true;
        }

        public static void AddSingle(
            HierarchyBindingsNode node,
            HierarchyBindingsChildScan scan,
            IdentifierScope used,
            HashSet<string> siblingNames,
            List<string> errors)
        {
            if (scan.Boundaries != null)
            {
                HierarchyBindingsBoundaryMembers.AddBoundary(node, scan, used, siblingNames, errors);
                return;
            }

            var propertyName = HierarchyBindingsNaming.ToIdentifier(scan.Child.name);

            if (HierarchyBindingsMemberNames.Validate(propertyName, scan.Child.name, scan.ChildPath, used, errors) == false)
                return;

            propertyName = HierarchyBindingsMemberNames.DeclareMember(propertyName, used);

            // The nested class name lives in the same namespace as the properties,
            // so it is also deduplicated through the shared set of taken names.
            scan.Node.SetNames(
                HierarchyBindingsMemberNames.DeclareType(propertyName + "Bindings", used, scan.Node.Members),
                propertyName,
                HierarchyBindingsNaming.ToFieldName(propertyName));
            node.Children.Add(scan.Node);
        }
    }
}
