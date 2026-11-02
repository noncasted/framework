using System.Text;
using UnityEngine;

namespace Internal
{
    // The comment over a generated class that shows which object is behind which property.
    internal static class HierarchyBindingsHierarchyMap
    {
        // Hierarchy map in a comment: the class fields are flat but the mirror is not, and without it
        // it is unclear which object is behind which property. The order comes from the transforms themselves,
        // so the comment reads the same way as the Hierarchy window.
        public static void AppendHierarchy(
            StringBuilder builder,
            HierarchyBindingsNode node,
            string indent,
            bool withHeader)
        {
            if (node.Target == null || node.Target.transform.childCount == 0)
                return;

            // The branch was cut off by a marker - nothing to show, the mirror has no children.
            if (node.IgnoreChildren == true)
                return;

            // For nested classes the object name is already on the line above - as a path from the root.
            if (withHeader)
                builder.AppendLine($"{indent}// {node.Target.name}");

            AppendHierarchyChildren(builder, node, indent, 1);
        }

        private static void AppendHierarchyChildren(
            StringBuilder builder,
            HierarchyBindingsNode node,
            string indent,
            int depth)
        {
            var transform = node.Target.transform;
            var padding = new string(' ', depth * 4 - 2);

            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index).gameObject;

                if (TryFindGroup(node, child, out var group, out var boundary, out var elementIndex))
                {
                    // A row is shown as one line on the first element: in the class it is a single
                    // array anyway, and a couple dozen identical lines only clutter the map.
                    if (elementIndex > 0)
                        continue;

                    var count = group != null ? group.Elements.Count : boundary.Targets.Count;
                    var last = group != null
                        ? ElementName(group, count - 1)
                        : TargetName(boundary.Targets[count - 1]);
                    var suffix = group != null ? string.Empty : ", own bindings";
                    builder.AppendLine($"{indent}// {padding}- {child.name} .. {last} ({count} items{suffix})");

                    if (group != null && group.IgnoreChildren == false)
                        AppendHierarchyChildren(builder, group, indent, depth + 1);

                    continue;
                }

                var childNode = FindChild(node, child);

                if (childNode == null)
                {
                    // The child is not in the mirror as a node: either it has its own bindings (a field per
                    // class, we do not go inside), or on a variant it belongs to the base entirely.
                    if (HasBoundaryFields(node, child) == false)
                        continue;

                    builder.AppendLine($"{indent}// {padding}- {child.name} (own bindings)");
                    continue;
                }

                if (childNode.IgnoreChildren == true && child.transform.childCount > 0)
                {
                    builder.AppendLine($"{indent}// {padding}- {child.name} (children ignored)");
                    continue;
                }

                builder.AppendLine($"{indent}// {padding}- {child.name}");
                AppendHierarchyChildren(builder, childNode, indent, depth + 1);
            }
        }

        // Collapsed row: either an array of nested classes (then return the template node), or an
        // array of references to other bindings (then return the field). The position is needed so the line
        // is printed once, on the first element.
        private static bool TryFindGroup(
            HierarchyBindingsNode node,
            GameObject child,
            out HierarchyBindingsNode group,
            out HierarchyBindingsField boundary,
            out int elementIndex)
        {
            group = null;
            boundary = null;
            elementIndex = 0;

            foreach (var candidate in node.Children)
            {
                if (candidate.IsArray == false)
                    continue;

                for (var index = 0; index < candidate.Elements.Count; index++)
                {
                    if (candidate.Elements[index].Target != child)
                        continue;

                    group = candidate;
                    elementIndex = index;
                    return true;
                }
            }

            foreach (var field in node.Fields)
            {
                for (var index = 0; index < field.Targets.Count; index++)
                {
                    var component = field.Targets[index] as Component;

                    if (component == null || component.gameObject != child)
                        continue;

                    boundary = field;
                    elementIndex = index;
                    return true;
                }
            }

            return false;
        }

        private static bool HasBoundaryFields(HierarchyBindingsNode node, GameObject child)
        {
            foreach (var field in node.Fields)
            {
                if (field.Target is Component component && component.gameObject == child)
                    return true;
            }

            return false;
        }

        public static string ElementName(HierarchyBindingsNode group, int index)
        {
            return TargetName(group.Elements[index].Target);
        }

        private static string TargetName(UnityEngine.Object target)
        {
            if (target is Component component)
                return component.gameObject.name;

            return target == null ? "?" : target.name;
        }

        private static HierarchyBindingsNode FindChild(HierarchyBindingsNode node, GameObject child)
        {
            foreach (var candidate in node.Children)
            {
                if (candidate.Target == child)
                    return candidate;
            }

            return null;
        }
    }
}
