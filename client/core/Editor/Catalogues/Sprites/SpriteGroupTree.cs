using System;
using System.Collections.Generic;

namespace Internal
{
    internal static class SpriteGroupTree
    {
        private static readonly ILog _log = Logs.Of(typeof(SpriteGroupTree));

        // Group path "Game.Players" is laid out as a tree: Sprites.Game.Players.
        // An intermediate node without its own group is just a container for nested ones.
        internal static SpriteGroupNode Build(IReadOnlyList<SpriteGroupDefinition> groups)
        {
            var root = new SpriteGroupNode(string.Empty, string.Empty);

            foreach (var group in groups)
            {
                var node = root;

                foreach (var segment in group.Path.Split('.'))
                {
                    if (node.Children.TryGetValue(segment, out var child) == false)
                    {
                        var path = node.Path.Length == 0 ? segment : node.Path + "." + segment;
                        child = new SpriteGroupNode(segment, path);
                        node.Children.Add(segment, child);
                    }

                    node = child;
                }

                node.SetGroup(group);
            }

            foreach (var node in EnumerateNodes(root))
                RemoveConflictingChildren(node);

            return root;
        }

        private static void RemoveConflictingChildren(SpriteGroupNode node)
        {
            if (node.Group == null)
                return;

            var members = new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in node.Group.Properties)
                members.Add(property.Owner ?? property.PropertyName);

            var conflicts = new List<string>();

            foreach (var name in node.Children.Keys)
            {
                if (members.Contains(name))
                    conflicts.Add(name);
            }

            foreach (var name in conflicts)
            {
                _log.Error("Group '{Parent}.{Name}' conflicts with a sprite of group '{Group}'. Skipping the nested group.",
                    node.Path, name, node.Path);
                node.Children.Remove(name);
            }
        }

        internal static IEnumerable<SpriteGroupNode> EnumerateNodes(SpriteGroupNode root)
        {
            foreach (var child in root.Children.Values)
            {
                yield return child;

                foreach (var nested in EnumerateNodes(child))
                    yield return nested;
            }
        }
    }
}
