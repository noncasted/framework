using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Internal
{
    // What a row of siblings is compared by: the contents without the name of the object.
    internal static class HierarchyBindingsSignature
    {
        // A row collapses only when the siblings carry the same set of bindings types. The order of the
        // components on the objects is not part of the signature.
        public static string ComputeBoundarySignature(List<Component> boundaries)
        {
            var types = boundaries
                .Select(boundary => HierarchyBindingsMemberNames.ToCodeTypeName(boundary))
                .OrderBy(name => name, StringComparer.Ordinal);

            return "boundary:" + string.Join(",", types);
        }

        // Node contents without its own name: decides whether one class serves
        // all siblings at once.
        public static string ComputeSignature(HierarchyBindingsNode node)
        {
            var builder = new StringBuilder();
            AppendSignature(builder, node);
            return builder.ToString();
        }

        private static void AppendSignature(StringBuilder builder, HierarchyBindingsNode node)
        {
            foreach (var field in node.Fields)
                builder.Append(field.PropertyName).Append(':').Append(field.DeclaredTypeName).Append(';');

            foreach (var child in node.Children)
            {
                builder.Append(child.PropertyName).Append(child.IsArray ? "[" + child.Elements.Count + "]" : "");
                builder.Append('{');
                AppendSignature(builder, child);
                builder.Append('}');
            }
        }
    }
}
