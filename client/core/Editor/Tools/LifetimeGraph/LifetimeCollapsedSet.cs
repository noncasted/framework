using System.Collections.Generic;

namespace Internal
{
    // The nodes of the lifetime graph with a hidden subtree.
    internal sealed class LifetimeCollapsedSet
    {
        private readonly HashSet<int> _ids = new();

        public bool Contains(int id)
        {
            return _ids.Contains(id);
        }

        public void Toggle(int id)
        {
            if (_ids.Remove(id) == false)
                _ids.Add(id);
        }

        public void Clear()
        {
            _ids.Clear();
        }

        public void RemoveDead(IReadOnlyDictionary<int, LifetimeRecord> alive)
        {
            _ids.RemoveWhere(id => alive.ContainsKey(id) == false);
        }

        // Visible are the roots and everything under them, except subtrees of collapsed nodes (the collapsed node itself is visible).
        public HashSet<int> CollectVisible(LifetimeTree tree)
        {
            var visible = new HashSet<int>();
            var pending = new Stack<int>(tree.Roots);

            while (pending.Count > 0)
            {
                var id = pending.Pop();
                visible.Add(id);

                if (_ids.Contains(id) == true)
                    continue;

                foreach (var child in tree.ChildrenOf(id))
                    pending.Push(child);
            }

            return visible;
        }
    }
}
