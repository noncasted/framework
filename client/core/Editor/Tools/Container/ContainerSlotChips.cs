using System;
using UnityEngine.UIElements;

namespace Internal
{
    // A button of a registration: the sections of the selected slot show them, a click selects the slot.
    internal sealed class ContainerSlotChips
    {
        public ContainerSlotChips(ContainerRegistrations registrations, Action<int> selectSlot)
        {
            _registrations = registrations;
            _selectSlot = selectSlot;
        }

        private readonly ContainerRegistrations _registrations;
        private readonly Action<int> _selectSlot;

        public Button Make(int slot, bool selected)
        {
            var hasInfo = _registrations.TryGet(slot, out var info);
            var title = hasInfo ? ContainerGraphRead.TypeName(info.ImplementationType) : "(missing)";
            var lifetime = hasInfo ? info.Lifetime.ToString() : string.Empty;

            var text = string.IsNullOrEmpty(lifetime)
                ? $"[{slot}] {title}"
                : $"[{slot}] {title} · {lifetime}";

            var button = new Button(() => _selectSlot(slot)) { text = text };
            button.AddToClassList("slot-chip");

            if (selected)
                button.AddToClassList("slot-chip--selected");

            if (hasInfo)
            {
                button.tooltip =
                    $"Slot {slot}\n{ContainerGraphRead.TypeName(info.ImplementationType)}\n{ContainerGraphRead.ServiceTypes(info.ServiceTypes)}\n" +
                    $"Lifetime {info.Lifetime}\nInstantiated {ContainerDebuggerFormat.Flag(info.IsInstantiated)}";
            }
            else
            {
                button.tooltip = $"Slot {slot} is not in Registrations";
            }

            return button;
        }
    }
}
