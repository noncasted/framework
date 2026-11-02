using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Internal
{
    // The table of registrations; its selection is the selected slot.
    internal sealed class ContainerRegistrationsSection
    {
        public ContainerRegistrationsSection(
            ContainerDebuggerSelection selection,
            ContainerRegistrations registrations,
            Func<Type, string> formatPhaseBytes,
            Action onSlotChanged)
        {
            _selection = selection;
            _registrations = registrations;
            _formatPhaseBytes = formatPhaseBytes;
            _onSlotChanged = onSlotChanged;
        }

        private readonly ContainerDebuggerSelection _selection;
        private readonly ContainerRegistrations _registrations;
        private readonly Func<Type, string> _formatPhaseBytes;
        private readonly Action _onSlotChanged;

        private MultiColumnListView _view;

        public VisualElement Build()
        {
            var section = ContainerDebuggerElements.Section("registrations-section", "Registrations");

            var columns = new Columns();
            columns.Add(MakeColumn("slot", "Slot", 56, false, info => info.Slot.ToString()));

            columns.Add(MakeColumn("implementation", "Implementation", 220, false,
                info => ContainerGraphRead.TypeName(info.ImplementationType)));

            columns.Add(MakeColumn("services", "Service types", 240, true,
                info => ContainerGraphRead.ServiceTypes(info.ServiceTypes)));
            columns.Add(MakeColumn("lifetime", "Lifetime", 90, false, info => info.Lifetime.ToString()));

            columns.Add(MakeColumn("instantiated", "Instantiated", 90, false,
                info => ContainerDebuggerFormat.Flag(info.IsInstantiated)));

            columns.Add(MakeColumn("allocated", "Phases alloc", 90, false,
                info => _formatPhaseBytes(info.ImplementationType)));

            _view = ContainerDebuggerElements.Table(columns, _registrations.Items, SelectionType.Single);
            _view.selectedIndicesChanged += OnSelected;
            section.Add(_view);

            return section;
        }

        public void Rebuild()
        {
            _view.Rebuild();
        }

        // The rows are rebuilt: the slot stays selected while it is still in the container.
        public void RestoreSelection()
        {
            var selectedIndex = _registrations.IndexOfSlot(_selection.Slot);

            if (selectedIndex >= 0)
                _view.selectedIndex = selectedIndex;
            else
            {
                _selection.SelectSlot(-1);
                _view.ClearSelection();
            }
        }

        public void ShowSelectedSlot()
        {
            var index = _registrations.IndexOfSlot(_selection.Slot);

            if (index >= 0 && _view.selectedIndex != index)
                _view.selectedIndex = index;
        }

        private Column MakeColumn(string name, string title, float width, bool stretchable, Func<RegistrationInfo, string> read)
        {
            return ContainerDebuggerElements.Column(name, title, width, 48, stretchable, _registrations.Items, read, FormatTooltip);
        }

        private void OnSelected(IEnumerable<int> indices)
        {
            if (_selection.IsRefreshing)
                return;

            var index = ContainerDebuggerElements.FirstIndex(indices);
            var items = _registrations.Items;

            _selection.SelectSlot(index >= 0 && index < items.Count ? items[index].Slot : -1);
            _onSlotChanged();
        }

        private static string FormatTooltip(RegistrationInfo info)
        {
            return $"{ContainerGraphRead.TypeName(info.ImplementationType)}\n{ContainerGraphRead.ServiceTypes(info.ServiceTypes)}";
        }
    }
}
