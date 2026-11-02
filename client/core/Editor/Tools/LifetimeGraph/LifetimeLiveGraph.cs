using System;
using System.Collections.Generic;
using System.IO;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Live Graph Toolkit tree of alive lifetimes from <see cref="LifetimeTracking"/>.
    /// The graph asset is a transient scratch file, not a source of truth.
    /// </summary>
    [Serializable]
    [Graph(AssetExtension)]
    public sealed class LifetimeLiveGraph : Graph
    {
        public const string AssetExtension = "lifetimegraph";

        internal static string AssetPath => FrameworkSettings.Content("Editor/Transient/LifetimeLiveGraph.lifetimegraph");

        private const double SyncInterval = 0.5;

        [NonSerialized]
        private Dictionary<int, LifetimeNode> _nodes;

        [NonSerialized]
        private int _syncedVersion = -1;

        [NonSerialized]
        private double _nextSyncAt;

        [NonSerialized]
        private bool _isSyncing;

        [NonSerialized]
        private LifetimeCollapsedSet _collapsed;

        public string Status { get; private set; } = "No lifetimes";

        [MenuItem(FrameworkMenu.LifetimeGraph + "Open", priority = FrameworkMenu.GroupPriority)]
        public static void Open()
        {
            var graph = EnsureGraph();
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetPath);

            AssetDatabase.OpenAsset(asset);
            graph.ForceSync();
        }

        public override void OnEnable()
        {
            base.OnEnable();

            _nodes = null;
            _collapsed ??= new LifetimeCollapsedSet();
            _syncedVersion = -1;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        public override void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
            _nodes = null;
            base.OnDisable();
        }

        internal void ForceSync()
        {
            _syncedVersion = -1;
            Sync();
        }

        internal void ClearTracked()
        {
            LifetimeTracking.Clear();
            _collapsed.Clear();
            ForceSync();
        }

        internal bool IsCollapsed(int lifetimeId)
        {
            return _collapsed.Contains(lifetimeId);
        }

        // Button on a node: the sync is deferred so nodes are not recreated inside their own view handler.
        internal void ToggleCollapsed(int lifetimeId)
        {
            _collapsed.Toggle(lifetimeId);

            EditorApplication.delayCall += ForceSync;
        }

        private void OnUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextSyncAt)
                return;

            _nextSyncAt = EditorApplication.timeSinceStartup + SyncInterval;

            if (LifetimeTracking.Version == _syncedVersion)
                return;

            if (GraphWindows.IsDisplayed(this) == false)
                return;

            Sync();
        }

        private void Sync()
        {
            if (_isSyncing == true)
                return;

            _isSyncing = true;
            UndoBeginRecordGraph("Refresh lifetime tree");

            try
            {
                SyncNodes();
            }
            finally
            {
                UndoEndRecordGraph();
                _isSyncing = false;
            }
        }

        private void SyncNodes()
        {
            var alive = LifetimeTracking.Alive;

            // After OnEnable (domain reload, reopening) the node mapping is lost: rebuild from scratch.
            if (_nodes == null)
            {
                ClearNodes();
                _nodes = new Dictionary<int, LifetimeNode>();
            }

            _collapsed.RemoveDead(alive);

            var tree = new LifetimeTree(alive);
            var visible = _collapsed.CollectVisible(tree);

            RemoveHidden(visible);
            AddVisible(visible, tree, alive);
            var orphans = RefreshPresentation(tree, alive);
            LifetimeTreeLayout.Apply(tree, _nodes, _collapsed);

            _syncedVersion = LifetimeTracking.Version;
            Status = FormatStatus(alive, visible.Count, orphans);
        }

        private void RemoveHidden(HashSet<int> visible)
        {
            var hidden = new List<int>();

            foreach (var id in _nodes.Keys)
            {
                if (visible.Contains(id) == false)
                    hidden.Add(id);
            }

            foreach (var id in hidden)
            {
                RemoveNode(_nodes[id]);
                _nodes.Remove(id);
            }
        }

        private void AddVisible(HashSet<int> visible, LifetimeTree tree, IReadOnlyDictionary<int, LifetimeRecord> alive)
        {
            var added = new List<int>();

            foreach (var id in visible)
            {
                if (_nodes.ContainsKey(id) == false)
                    added.Add(id);
            }

            // The parent is created before the child, so it has a smaller id: in ascending order it is already in the graph.
            added.Sort();

            foreach (var id in added)
            {
                var record = alive[id];
                var node = new LifetimeNode();

                node.Prepare(record);
                AddNode(node);
                _nodes.Add(id, node);

                ConnectParent(id, record.ParentId, LifetimeNode.ParentPortName);
                ConnectParent(id, record.SecondParentId, LifetimeNode.SecondParentPortName);
            }

            // The second parent may appear after the child (its subtree was expanded): add the wires to it.
            foreach (var id in added)
            {
                foreach (var child in tree.IntersectionsOf(id))
                    ConnectParent(child, id, LifetimeNode.SecondParentPortName);
            }
        }

        // The second Intersect parent is drawn dashed: the tree layout follows only the main one.
        private void ConnectParent(int childId, int parentId, string portName)
        {
            if (_nodes.TryGetValue(parentId, out var parent) == false)
                return;

            if (_nodes.TryGetValue(childId, out var child) == false)
                return;

            var output = parent.GetOutputPortByName(LifetimeNode.ChildrenPortName);
            var input = child.GetInputPortByName(portName);

            // Both Intersect parents may be added in the same sync: the wire is already drawn by the first pass,
            // and a repeated Connect to a Single port throws.
            if (input.IsConnected == true)
                return;

            Connect(output, input);

            if (portName == LifetimeNode.SecondParentPortName)
                GetWire(output, input).IsDashed = true;
        }

        private int RefreshPresentation(LifetimeTree tree, IReadOnlyDictionary<int, LifetimeRecord> alive)
        {
            var orphans = 0;

            foreach (var pair in _nodes)
            {
                var record = alive[pair.Key];
                var isOrphan = IsOrphan(record, alive);

                if (isOrphan == true)
                    orphans++;

                pair.Value.Apply(
                    record,
                    isOrphan,
                    tree.DescendantCount(pair.Key),
                    _collapsed.Contains(pair.Key));
            }

            return orphans;
        }

        private void ClearNodes()
        {
            var existing = new List<INode>(GetNodes());

            foreach (var node in existing)
                RemoveNode(node);
        }

        private static bool IsOrphan(LifetimeRecord record, IReadOnlyDictionary<int, LifetimeRecord> alive)
        {
            return record.ParentId != 0 && alive.ContainsKey(record.ParentId) == false;
        }

        private static string FormatStatus(IReadOnlyDictionary<int, LifetimeRecord> alive, int shown, int orphans)
        {
            var tracking = LifetimeAnalytics.IsEnabled ? "tracking" : "not tracking";
            return $"{alive.Count} alive · {shown} shown · {orphans} orphan(s) · {tracking}";
        }

        private static LifetimeLiveGraph EnsureGraph()
        {
            var directory = Path.GetDirectoryName(Path.Combine(Application.dataPath, "..", AssetPath));
            Directory.CreateDirectory(directory);

            if (File.Exists(Path.Combine(Application.dataPath, "..", AssetPath)) == true)
                return GraphDatabase.LoadGraph<LifetimeLiveGraph>(AssetPath);

            return GraphDatabase.CreateGraph<LifetimeLiveGraph>(AssetPath);
        }
    }
}
