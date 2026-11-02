using UnityEngine.UIElements;

namespace Internal
{
    // What the selected slot depends on and what uses it.
    internal sealed class ContainerDependenciesSection
    {
        public ContainerDependenciesSection(
            ContainerDebuggerSelection selection,
            ContainerRegistrations registrations,
            ContainerSlotChips chips)
        {
            _selection = selection;
            _registrations = registrations;
            _chips = chips;
        }

        private readonly ContainerDebuggerSelection _selection;
        private readonly ContainerRegistrations _registrations;
        private readonly ContainerSlotChips _chips;

        private VisualElement _body;

        public VisualElement Build()
        {
            var section = ContainerDebuggerElements.Section("graph-section", "Dependencies");
            _body = ContainerDebuggerElements.ScrollBody(section);
            return section;
        }

        public void Clear()
        {
            _body.Clear();
        }

        public void Rebuild()
        {
            _body.Clear();

            var selectedSlot = _selection.Slot;

            if (selectedSlot < 0)
            {
                _body.Add(ContainerDebuggerElements.Hint("Select a registration to inspect its dependencies."));
                return;
            }

            if (_registrations.TryGet(selectedSlot, out var selected) == false)
            {
                _body.Add(ContainerDebuggerElements.Hint($"Slot {selectedSlot} is not in this container."));
                return;
            }

            _body.Add(ContainerDebuggerElements.Caption("Depends on"));

            var dependencies = selected.Dependencies;
            var hasDependencies = dependencies != null && dependencies.Count > 0;

            if (hasDependencies == false)
                _body.Add(ContainerDebuggerElements.Hint("No dependencies."));
            else
            {
                foreach (var slot in dependencies)
                    _body.Add(_chips.Make(slot, slot == selectedSlot));
            }

            _body.Add(ContainerDebuggerElements.Caption("Selected"));
            _body.Add(_chips.Make(selectedSlot, true));

            _body.Add(ContainerDebuggerElements.Caption("Used by"));

            var usedBy = 0;

            foreach (var info in _registrations.Items)
            {
                var slots = info.Dependencies;

                if (slots == null)
                    continue;

                foreach (var slot in slots)
                {
                    if (slot != selectedSlot)
                        continue;

                    _body.Add(_chips.Make(info.Slot, false));
                    usedBy++;
                    break;
                }
            }

            if (usedBy == 0)
                _body.Add(ContainerDebuggerElements.Hint("Nothing in this container depends on it."));
        }
    }
}
