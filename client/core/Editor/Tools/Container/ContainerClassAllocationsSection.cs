using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Internal
{
    // Totals survive container disposal: short-lived entity scopes are visible only here.
    internal sealed class ContainerClassAllocationsSection
    {
        private readonly List<ContainerClassAllocations> _items = new();

        private MultiColumnListView _view;

        public VisualElement Build()
        {
            var section = new VisualElement();
            section.AddToClassList("class-allocations");

            section.Add(ContainerDebuggerElements.PaneTitle(
                "Allocated since Play, by class",
                "Build + phases bytes of every container of the class, disposed ones included."));

            var columns = new Columns();
            columns.Add(MakeColumn("class", "Class", 140, true, totals => totals.Name));
            columns.Add(MakeColumn("built", "Built", 46, false, totals => totals.Built.ToString()));

            columns.Add(MakeColumn("bytes", "Bytes", 70, false,
                totals => ContainerDebuggerFormat.Bytes(totals.BuildBytes + totals.PhaseBytes)));

            _view = ContainerDebuggerElements.Table(columns, _items, SelectionType.None);
            section.Add(_view);

            return section;
        }

        public void Refresh()
        {
            _items.Clear();

            foreach (var totals in ContainerGraphRead.List(() => ContainerRegistryDebug.ClassAllocations))
                _items.Add(totals);

            _items.Sort((a, b) => (b.BuildBytes + b.PhaseBytes).CompareTo(a.BuildBytes + a.PhaseBytes));
            _view.Rebuild();
        }

        private Column MakeColumn(string name, string title, float width, bool stretchable, Func<ContainerClassAllocations, string> read)
        {
            return ContainerDebuggerElements.Column(name, title, width, 40, stretchable, _items, read, FormatTooltip);
        }

        private static string FormatTooltip(ContainerClassAllocations totals)
        {
            return $"{totals.Name}\nBuilt {totals.Built} time(s)\n" +
                   $"Build {ContainerDebuggerFormat.Bytes(totals.BuildBytes)} · Phases {ContainerDebuggerFormat.Bytes(totals.PhaseBytes)}";
        }
    }
}
