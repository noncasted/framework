using System.Collections.Generic;

namespace Internal
{
    // The registrations of the selected container: the data under the sections that show them.
    internal sealed class ContainerRegistrations
    {
        private readonly List<RegistrationInfo> _items = new();
        private readonly Dictionary<int, RegistrationInfo> _bySlot = new();

        public List<RegistrationInfo> Items => _items;

        public void Clear()
        {
            _items.Clear();
            _bySlot.Clear();
        }

        public void Fill(IContainerDiagnostics container)
        {
            foreach (var info in ContainerGraphRead.List(() => container.Registrations))
            {
                _items.Add(info);
                _bySlot[info.Slot] = info;
            }
        }

        public bool TryGet(int slot, out RegistrationInfo info)
        {
            return _bySlot.TryGetValue(slot, out info);
        }

        public int IndexOfSlot(int slot)
        {
            if (slot < 0)
                return -1;

            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].Slot == slot)
                    return i;
            }

            return -1;
        }
    }
}
