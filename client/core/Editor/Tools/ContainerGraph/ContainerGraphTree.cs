using System;
using System.Collections.Generic;

namespace Internal
{
    // The children of every container under the roots, read before the live graph is touched: a read that throws
    // reaches the graph, which keeps its old nodes instead of an empty graph saved as "No containers".
    internal static class ContainerGraphTree
    {
        public static Dictionary<IContainerDiagnostics, IReadOnlyList<IContainerDiagnostics>> ReadChildren(
            IReadOnlyList<IContainerDiagnostics> roots)
        {
            var children = new Dictionary<IContainerDiagnostics, IReadOnlyList<IContainerDiagnostics>>();
            var pending = new Stack<IContainerDiagnostics>();

            foreach (var root in roots)
            {
                if (root != null)
                    pending.Push(root);
            }

            while (pending.Count > 0)
            {
                var diagnostics = pending.Pop();

                if (children.ContainsKey(diagnostics) == true)
                    continue;

                var list = diagnostics.Children ?? Array.Empty<IContainerDiagnostics>();
                children.Add(diagnostics, list);

                foreach (var child in list)
                {
                    if (child != null)
                        pending.Push(child);
                }
            }

            return children;
        }
    }
}
