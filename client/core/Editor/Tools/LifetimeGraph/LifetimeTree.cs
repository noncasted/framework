using System;
using System.Collections.Generic;

namespace Internal
{
    /// <summary>
    /// Snapshot of the alive lifetime tree by primary parent, built once per graph sync.
    /// </summary>
    internal sealed class LifetimeTree
    {
        public LifetimeTree(IReadOnlyDictionary<int, LifetimeRecord> alive)
        {
            foreach (var record in alive.Values)
            {
                if (alive.ContainsKey(record.SecondParentId) == true)
                    AddTo(_intersections, record.SecondParentId, record.Id);

                if (alive.ContainsKey(record.ParentId) == false)
                {
                    _roots.Add(record.Id);
                    continue;
                }

                AddTo(_children, record.ParentId, record.Id);
            }

            _roots.Sort();

            foreach (var list in _children.Values)
                list.Sort();

            foreach (var root in _roots)
                CountDescendants(root);
        }

        private readonly List<int> _roots = new();
        private readonly Dictionary<int, List<int>> _children = new();
        private readonly Dictionary<int, List<int>> _intersections = new();
        private readonly Dictionary<int, int> _descendants = new();

        public IReadOnlyList<int> Roots => _roots;

        public IReadOnlyList<int> ChildrenOf(int id)
        {
            return _children.TryGetValue(id, out var list) ? list : Array.Empty<int>();
        }

        // Lifetimes whose second Intersect parent is id.
        public IReadOnlyList<int> IntersectionsOf(int id)
        {
            return _intersections.TryGetValue(id, out var list) ? list : Array.Empty<int>();
        }

        public int DescendantCount(int id)
        {
            return _descendants[id];
        }

        private int CountDescendants(int id)
        {
            var count = 0;

            foreach (var child in ChildrenOf(id))
                count += CountDescendants(child) + 1;

            _descendants[id] = count;
            return count;
        }

        private static void AddTo(Dictionary<int, List<int>> map, int key, int value)
        {
            if (map.TryGetValue(key, out var list) == false)
            {
                list = new List<int>();
                map.Add(key, list);
            }

            list.Add(value);
        }
    }
}
