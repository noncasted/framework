using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Internal
{
    // The allocations of the selected container; choosing a listener selects its registration.
    internal sealed class ContainerAllocationsSection
    {
        public ContainerAllocationsSection(ContainerDebuggerSelection selection, Action<Type> onListenerSelected)
        {
            _selection = selection;
            _onListenerSelected = onListenerSelected;
        }

        private readonly ContainerDebuggerSelection _selection;
        private readonly Action<Type> _onListenerSelected;
        private readonly List<AllocationRecord> _items = new();
        private readonly Dictionary<Type, long> _phaseBytesByType = new();

        private MultiColumnListView _view;
        private Label _hint;

        public VisualElement Build()
        {
            var section = ContainerDebuggerElements.Section(
                "allocations-section",
                "Allocations",
                "Managed bytes allocated on the main thread, editor only.\n" +
                "Build: generated constructor, every eager service included.\n" +
                "Phases: EventLoop callbacks per listener; async ones only up to the first await.\n" +
                "Numbers are inclusive: a callback that builds child scopes carries their bytes too.");

            _hint = new Label();
            _hint.AddToClassList("hint");
            section.Add(_hint);

            var columns = new Columns();
            columns.Add(MakeColumn("phase", "Phase", 150, false, ContainerDebuggerFormat.Phase));
            columns.Add(MakeColumn("listener", "Listener", 200, true, ContainerDebuggerFormat.Listener));
            columns.Add(MakeColumn("calls", "Calls", 50, false, record => record.Calls.ToString()));
            columns.Add(MakeColumn("bytes", "Bytes", 80, false, record => ContainerDebuggerFormat.Bytes(record.Bytes)));

            _view = ContainerDebuggerElements.Table(columns, _items, SelectionType.Single);
            _view.selectedIndicesChanged += OnSelected;
            section.Add(_view);

            return section;
        }

        public void Clear()
        {
            _items.Clear();
            _phaseBytesByType.Clear();
            _view.Rebuild();
        }

        public void Rebuild(IContainerDiagnostics container)
        {
            _items.Clear();
            _phaseBytesByType.Clear();

            long buildBytes = 0;
            long phaseBytes = 0;

            foreach (var record in ContainerGraphRead.List(() => container.Allocations))
            {
                _items.Add(record);

                if (record.Phase == null)
                {
                    buildBytes += record.Bytes;
                    continue;
                }

                phaseBytes += record.Bytes;

                if (record.Listener == null)
                    continue;

                _phaseBytesByType.TryGetValue(record.Listener, out var bytes);
                _phaseBytesByType[record.Listener] = bytes + record.Bytes;
            }

            _items.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
            _view.Rebuild();

            var containers = 0;
            var subtreeBytes = ContainerDebuggerFormat.SubtreeBytes(container, ref containers, 0);

            _hint.text =
                $"Build {ContainerDebuggerFormat.Bytes(buildBytes)} · Phases {ContainerDebuggerFormat.Bytes(phaseBytes)} · " +
                $"With children {ContainerDebuggerFormat.Bytes(subtreeBytes)} ({containers} container(s))";
        }

        public string FormatPhaseBytes(Type implementation)
        {
            return implementation != null && _phaseBytesByType.TryGetValue(implementation, out var bytes)
                ? ContainerDebuggerFormat.Bytes(bytes)
                : "—";
        }

        private Column MakeColumn(string name, string title, float width, bool stretchable, Func<AllocationRecord, string> read)
        {
            return ContainerDebuggerElements.Column(name, title, width, 40, stretchable, _items, read, FormatTooltip);
        }

        private void OnSelected(IEnumerable<int> indices)
        {
            if (_selection.IsRefreshing)
                return;

            var index = ContainerDebuggerElements.FirstIndex(indices);

            if (index < 0 || index >= _items.Count)
                return;

            var listener = _items[index].Listener;

            if (listener == null)
                return;

            _onListenerSelected(listener);
        }

        private static string FormatTooltip(AllocationRecord record)
        {
            return $"{ContainerDebuggerFormat.Phase(record)} · {ContainerDebuggerFormat.Listener(record)}\n" +
                   $"{record.Bytes} B in {record.Calls} call(s)";
        }
    }
}
