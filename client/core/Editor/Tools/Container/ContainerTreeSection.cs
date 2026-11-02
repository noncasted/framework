using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    // The tree of live containers; choosing a node selects the container.
    internal sealed class ContainerTreeSection
    {
        public ContainerTreeSection(ContainerDebuggerSelection selection, Action onContainerChanged)
        {
            _selection = selection;
            _onContainerChanged = onContainerChanged;
        }

        private readonly ContainerDebuggerSelection _selection;
        private readonly Action _onContainerChanged;
        private readonly Dictionary<int, IContainerDiagnostics> _diagnosticsById = new();
        private readonly HashSet<IContainerDiagnostics> _visited = new();

        private int _nextId = 1;
        private TreeView _treeView;
        private Label _emptyLabel;

        public int RootCount { get; private set; }
        public IReadOnlyCollection<IContainerDiagnostics> Containers => _diagnosticsById.Values;

        public void AddTo(VisualElement pane)
        {
            pane.Add(ContainerDebuggerElements.PaneTitle("Containers"));

            _treeView = new TreeView
            {
                fixedItemHeight = 22,
                selectionType = SelectionType.Single,
                autoExpand = true
            };
            _treeView.AddToClassList("container-tree");

            _treeView.makeItem = () => {
                var label = new Label();
                label.AddToClassList("tree-item");
                return label;
            };
            _treeView.bindItem = BindItem;
            _treeView.selectedIndicesChanged += OnSelectionChanged;
            pane.Add(_treeView);

            _emptyLabel = new Label();
            _emptyLabel.AddToClassList("empty-state");
            pane.Add(_emptyLabel);
        }

        public void Refresh()
        {
            var roots = ContainerGraphRead.List(() => ContainerRegistryDebug.Roots);
            var items = BuildItems(roots);

            RootCount = roots.Count;

            _treeView.SetRootItems(items);
            _treeView.Rebuild();
            _treeView.ExpandAll();

            var hasRoots = items.Count > 0;
            _treeView.style.display = hasRoots ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyLabel.style.display = hasRoots ? DisplayStyle.None : DisplayStyle.Flex;
            _emptyLabel.text = "No containers";

            var selectedId = FindId(_selection.Container);

            if (selectedId < 0 && items.Count > 0)
            {
                selectedId = items[0].id;
                _selection.SelectContainer(items[0].data);
            }

            if (selectedId < 0)
                _selection.SelectContainer(null);

            if (selectedId >= 0)
                _treeView.SetSelectionById(selectedId);
            else
                _treeView.ClearSelection();
        }

        private List<TreeViewItemData<IContainerDiagnostics>> BuildItems(IReadOnlyList<IContainerDiagnostics> roots)
        {
            _diagnosticsById.Clear();
            _visited.Clear();
            _nextId = 1;

            var items = new List<TreeViewItemData<IContainerDiagnostics>>();

            foreach (var root in roots)
            {
                if (root == null)
                    continue;

                items.Add(BuildNode(root));
            }

            return items;
        }

        private TreeViewItemData<IContainerDiagnostics> BuildNode(IContainerDiagnostics diagnostics)
        {
            var id = _nextId++;
            _diagnosticsById[id] = diagnostics;

            var children = new List<TreeViewItemData<IContainerDiagnostics>>();

            if (_visited.Add(diagnostics) == false)
                return new TreeViewItemData<IContainerDiagnostics>(id, diagnostics, children);

            foreach (var child in ContainerGraphRead.List(() => diagnostics.Children))
            {
                if (child == null)
                    continue;

                children.Add(BuildNode(child));
            }

            return new TreeViewItemData<IContainerDiagnostics>(id, diagnostics, children);
        }

        private void BindItem(VisualElement element, int index)
        {
            var label = (Label)element;

            try
            {
                var diagnostics = _treeView.GetItemDataForIndex<IContainerDiagnostics>(index);
                label.text = ContainerDebuggerFormat.ContainerLabel(diagnostics);
                label.tooltip = ContainerDebuggerFormat.Path(diagnostics);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                label.text = "(error)";
                label.tooltip = exception.Message;
            }
        }

        private void OnSelectionChanged(IEnumerable<int> indices)
        {
            if (_selection.IsRefreshing)
                return;

            var index = ContainerDebuggerElements.FirstIndex(indices);
            IContainerDiagnostics next = null;

            if (index >= 0)
            {
                try
                {
                    next = _treeView.GetItemDataForIndex<IContainerDiagnostics>(index);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            if (ReferenceEquals(_selection.Container, next))
                return;

            _selection.SelectContainer(next);
            _onContainerChanged();
        }

        private int FindId(IContainerDiagnostics diagnostics)
        {
            if (diagnostics == null)
                return -1;

            foreach (var pair in _diagnosticsById)
            {
                if (ReferenceEquals(pair.Value, diagnostics))
                    return pair.Key;
            }

            return -1;
        }
    }
}
