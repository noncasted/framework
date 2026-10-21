using System.Security.Cryptography;
using System.Text;

namespace Internal
{
    // A structure snapshot is written into the generated component so that later we can tell
    // "same hierarchy" from "the hierarchy was changed but Regenerate was not pressed".
    internal static class HierarchyBindingsStructure
    {
        public static string ComputeHash(HierarchyBindingsNode root)
        {
            var builder = new StringBuilder();
            Append(builder, root);

            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            return System.BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void Append(StringBuilder builder, HierarchyBindingsNode node)
        {
            builder.Append(node.HierarchyPath).Append('|').Append(node.TypeName).Append(';');

            foreach (var field in node.Fields)
            {
                builder.Append(field.PropertyName).Append(':').Append(field.DeclaredTypeName).Append(';');

                // For an array all elements go into the snapshot: otherwise a rename or reorder
                // inside the row would go unnoticed.
                foreach (var target in field.Targets)
                    builder.Append(target == null ? "null" : target.name).Append(',');
            }

            foreach (var child in node.Children)
            {
                builder.Append(child.PropertyName).Append(':').Append(child.TypeName);

                if (child.IsArray == false)
                {
                    builder.Append(';');
                    Append(builder, child);
                    continue;
                }

                builder.Append('[').Append(child.Elements.Count).Append("];");

                foreach (var element in child.Elements)
                    Append(builder, element);
            }
        }
    }
}