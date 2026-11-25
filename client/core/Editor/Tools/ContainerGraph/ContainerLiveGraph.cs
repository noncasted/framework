using System;
using System.Collections.Generic;
using System.IO;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    /// <summary>
    /// Live Graph Toolkit view of <see cref="ContainerRegistryDebug.Roots"/>.
    /// The graph asset is a transient scratch file, not a source of truth.
    /// </summary>
    [Serializable]
    [Graph(AssetExtension)]
    public sealed class ContainerLiveGraph : Graph
    {
        public const string AssetExtension = "containergraph";
        public const string EmptyEditModeStatus = "No containers. Enter Play Mode to inspect a built graph.";
        public const string EmptyPlayModeStatus = "No containers yet. Waiting for a container Build.";

        internal static string AssetPath => FrameworkSettings.Content("Editor/Transient/ContainerLiveGraph.containergraph");

        private const float OriginX = 40f;

        private const float OriginY = 40f;

        // The tree grows left to right, following the Children -> Parent links: the column is the scope depth.
        private const float ColumnWidth = 480f;
        private const float SiblingGap = 24f;
        private const float RootGap = 64f;

        private static readonly ILog _log = Logs.Of<ContainerLiveGraph>();

        [NonSerialized]
        private Lifetime _lifetime;

        [NonSerialized]
        private bool _rebuildScheduled;

        [NonSerialized]
        private bool _isRebuilding;

        public string Status { get; private set; } = EmptyEditModeStatus;

        [MenuItem(FrameworkMenu.Container + "Graph", priority = FrameworkMenu.GroupPriority)]
        public static void Open()
        {
            try
            {
                var graph = ContainerLiveGraphAsset.Ensure();

                if (graph == null)
                    return;

                var path = GraphDatabase.GetGraphAssetPath(graph);

                if (string.IsNullOrEmpty(path))
                    path = AssetPath;

                var asset = AssetDatabase.LoadMainAssetAtPath(path);

                if (asset != null)
                    AssetDatabase.OpenAsset(asset);

                graph.Rebuild();
            }
            catch (Exception exception)
            {
                _log.Exception(exception);
            }
        }

        public override void OnEnable()
        {
            base.OnEnable();

            _lifetime?.Terminate();
            _lifetime = new Lifetime();

            try
            {
                ContainerRegistryDebug.Changed.Advise(_lifetime, ScheduleRebuild);
            }
            catch (Exception exception)
            {
                _log.Exception(exception);
            }

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ScheduleRebuild();
        }

        public override void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _lifetime?.Terminate();
            _lifetime = null;
            _rebuildScheduled = false;
            base.OnDisable();
        }

        internal void Rebuild()
        {
            if (_isRebuilding)
                return;

            _isRebuilding = true;

            try
            {
                RebuildNodes();
            }
            catch (Exception exception)
            {
                _log.Exception(exception);
                Status = "Failed to read container diagnostics";
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        private void ScheduleRebuild()
        {
            if (_rebuildScheduled)
                return;

            _rebuildScheduled = true;
            EditorApplication.delayCall += OnDelayedRebuild;
        }

        private void OnDelayedRebuild()
        {
            _rebuildScheduled = false;

            if (GraphWindows.IsDisplayed(this) == false)
                return;

            Rebuild();
        }

        private void OnPlayModeChanged(PlayModeStateChange _)
        {
            ScheduleRebuild();
        }

        private void RebuildNodes()
        {
            // Read before the graph is touched: a failure reaches Rebuild with the old nodes in place and nothing saved.
            var roots = ContainerRegistryDebug.Roots;
            var children = ContainerGraphTree.ReadChildren(roots);
            var nodes = new Dictionary<IContainerDiagnostics, ContainerNode>();
            var visited = new HashSet<IContainerDiagnostics>();

            UndoBeginRecordGraph("Refresh container tree");

            try
            {
                ClearNodes();

                var cursorY = OriginY;

                foreach (var root in roots)
                {
                    if (root == null)
                        continue;

                    var height = AddTree(root, null, OriginX, cursorY, children, nodes, visited);

                    if (height > 0f)
                        cursorY += height + RootGap;
                }

                ConnectParents(nodes);
            }
            finally
            {
                UndoEndRecordGraph();
            }

            Status = FormatStatus(roots.Count, nodes.Count);

            try
            {
                GraphDatabase.SaveGraph(this);
            }
            catch (Exception exception)
            {
                _log.Exception(exception);
            }
        }

        private float AddTree(
            IContainerDiagnostics diagnostics,
            IContainerDiagnostics parent,
            float x,
            float y,
            Dictionary<IContainerDiagnostics, IReadOnlyList<IContainerDiagnostics>> children,
            Dictionary<IContainerDiagnostics, ContainerNode> nodes,
            HashSet<IContainerDiagnostics> visited)
        {
            if (visited.Add(diagnostics) == false)
                return 0f;

            var node = new ContainerNode();
            node.Capture(diagnostics, parent);
            AddNode(node);
            node.Position = new Vector2(x, y);
            node.ApplyPresentation();
            nodes[diagnostics] = node;

            var childY = y;

            foreach (var child in children[diagnostics])
            {
                if (child == null)
                    continue;

                var height = AddTree(child, diagnostics, x + ColumnWidth, childY, children, nodes, visited);

                if (height > 0f)
                    childY += height + SiblingGap;
            }

            var childrenHeight = childY > y ? childY - y - SiblingGap : 0f;
            return Math.Max(node.EstimatedHeight, childrenHeight);
        }

        private void ConnectParents(Dictionary<IContainerDiagnostics, ContainerNode> nodes)
        {
            foreach (var pair in nodes)
            {
                var parent = ContainerGraphRead.Parent(pair.Key);

                if (parent == null)
                    continue;

                if (nodes.TryGetValue(parent, out var parentNode) == false)
                    continue;

                var output = parentNode.GetOutputPortByName(ContainerNode.ChildrenPortName);
                var input = pair.Value.GetInputPortByName(ContainerNode.ParentPortName);

                if (output == null || input == null)
                    continue;

                Connect(output, input);
            }
        }

        private void ClearNodes()
        {
            var existing = new List<INode>();

            foreach (var node in GetNodes())
                existing.Add(node);

            foreach (var node in existing)
                RemoveNode(node);
        }

        private static string FormatStatus(int rootCount, int containerCount)
        {
            if (rootCount == 0 || containerCount == 0)
            {
                return EditorApplication.isPlaying
                    ? EmptyPlayModeStatus
                    : EmptyEditModeStatus;
            }

            var mode = EditorApplication.isPlaying ? "Play Mode" : "Edit Mode";
            return $"{rootCount} root(s) · {containerCount} container(s) · {mode}";
        }
    }
}
