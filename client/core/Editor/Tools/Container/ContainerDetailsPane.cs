using System;
using UnityEngine.UIElements;

namespace Internal
{
    // The right pane of the debugger: composes the sections of the selected container.
    internal sealed class ContainerDetailsPane
    {
        public ContainerDetailsPane(ContainerDebuggerSelection selection)
        {
            _selection = selection;

            var chips = new ContainerSlotChips(_registrations, SelectSlot);

            _allocations = new ContainerAllocationsSection(selection, OnListenerSelected);
            _registrationsSection = new ContainerRegistrationsSection(selection, _registrations, _allocations.FormatPhaseBytes, RebuildSlotSections);
            _dependencies = new ContainerDependenciesSection(selection, _registrations, chips);
            _buildOrder = new ContainerBuildOrderSection(selection, chips);
        }

        private readonly ContainerDebuggerSelection _selection;
        private readonly ContainerRegistrations _registrations = new();
        private readonly ContainerAllocationsSection _allocations;
        private readonly ContainerRegistrationsSection _registrationsSection;
        private readonly ContainerDependenciesSection _dependencies;
        private readonly ContainerBuildOrderSection _buildOrder;

        private Label _emptyLabel;
        private Label _containerTitle;
        private VisualElement _details;

        public VisualElement Build()
        {
            var pane = new VisualElement();
            pane.AddToClassList("details-pane");

            _emptyLabel = new Label();
            _emptyLabel.AddToClassList("empty-state");
            pane.Add(_emptyLabel);

            _details = new VisualElement();
            _details.AddToClassList("details");

            _containerTitle = new Label();
            _containerTitle.AddToClassList("container-title");
            _details.Add(_containerTitle);

            _details.Add(_registrationsSection.Build());

            var lower = new VisualElement();
            lower.AddToClassList("lower");
            lower.Add(_dependencies.Build());
            lower.Add(_buildOrder.Build());
            _details.Add(lower);

            var bottom = new VisualElement();
            bottom.AddToClassList("bottom");
            bottom.Add(_allocations.Build());
            _details.Add(bottom);
            pane.Add(_details);

            return pane;
        }

        public void Rebuild()
        {
            var container = _selection.Container;
            var hasContainer = container != null;

            _details.style.display = hasContainer ? DisplayStyle.Flex : DisplayStyle.None;
            _emptyLabel.style.display = hasContainer ? DisplayStyle.None : DisplayStyle.Flex;
            _emptyLabel.text = ContainerDebuggerFormat.EmptyMessage();

            _registrations.Clear();

            if (hasContainer == false)
            {
                _allocations.Clear();
                _registrationsSection.Rebuild();
                _dependencies.Clear();
                _buildOrder.Clear();
                return;
            }

            _containerTitle.text = ContainerDebuggerFormat.Path(container);
            _containerTitle.tooltip = _containerTitle.text;

            // Before the registrations table is rebuilt: its "Phases alloc" column reads the allocations.
            _allocations.Rebuild(container);

            _registrations.Fill(container);
            _registrationsSection.Rebuild();
            _registrationsSection.RestoreSelection();

            RebuildSlotSections();
        }

        private void RebuildSlotSections()
        {
            _dependencies.Rebuild();
            _buildOrder.Rebuild();
        }

        private void SelectSlot(int slot)
        {
            _selection.SelectSlot(slot);
            _registrationsSection.ShowSelectedSlot();
            RebuildSlotSections();
        }

        private void OnListenerSelected(Type listener)
        {
            foreach (var info in _registrations.Items)
            {
                if (info.ImplementationType != listener)
                    continue;

                SelectSlot(info.Slot);
                return;
            }
        }
    }
}
