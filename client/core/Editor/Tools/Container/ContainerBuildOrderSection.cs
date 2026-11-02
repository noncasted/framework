using UnityEngine.UIElements;

namespace Internal
{
    // The order the selected container built its registrations in.
    internal sealed class ContainerBuildOrderSection
    {
        public ContainerBuildOrderSection(ContainerDebuggerSelection selection, ContainerSlotChips chips)
        {
            _selection = selection;
            _chips = chips;
        }

        private readonly ContainerDebuggerSelection _selection;
        private readonly ContainerSlotChips _chips;

        private VisualElement _body;

        public VisualElement Build()
        {
            var section = ContainerDebuggerElements.Section("build-section", "Build order");
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

            var container = _selection.Container;

            if (container == null)
            {
                _body.Add(ContainerDebuggerElements.Hint("No build order."));
                return;
            }

            var order = ContainerGraphRead.List(() => container.BuildOrder);

            if (order.Count == 0)
            {
                _body.Add(ContainerDebuggerElements.Hint("No build order."));
                return;
            }

            for (var i = 0; i < order.Count; i++)
            {
                var slot = order[i];
                var row = _chips.Make(slot, slot == _selection.Slot);
                row.text = $"{i + 1}. {row.text}";
                _body.Add(row);
            }
        }
    }
}
