using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    internal static class LifetimeTreeLayout
    {
        private const float OriginX = 40f;
        private const float OriginY = 40f;

        // The tree grows left to right: the column is the lifetime depth, the row is a subtree leaf.
        private const float ColumnWidth = 420f;
        private const float RowHeight = 110f;
        private const float RootGap = 60f;

        public static void Apply(LifetimeTree tree, IReadOnlyDictionary<int, LifetimeNode> nodes, LifetimeCollapsedSet collapsed)
        {
            var cursorY = OriginY;

            foreach (var root in tree.Roots)
            {
                var height = PlaceTree(root, 0, cursorY, tree, nodes, collapsed);
                cursorY += height + RootGap;
            }
        }

        // Returns the subtree height; a node sits at the level of its first child. A collapsed one is a leaf.
        private static float PlaceTree(
            int id,
            int depth,
            float y,
            LifetimeTree tree,
            IReadOnlyDictionary<int, LifetimeNode> nodes,
            LifetimeCollapsedSet collapsed)
        {
            var position = new Vector2(OriginX + depth * ColumnWidth, y);
            var node = nodes[id];

            if (node.Position != position)
                node.Position = position;

            var children = tree.ChildrenOf(id);

            if (children.Count == 0 || collapsed.Contains(id) == true)
                return RowHeight;

            var childY = y;

            foreach (var child in children)
                childY += PlaceTree(child, depth + 1, childY, tree, nodes, collapsed);

            return childY - y;
        }
    }
}
