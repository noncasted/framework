namespace Internal
{
    // What the sections of the debugger show. While the window refreshes it rewrites the views itself, so the selection
    // callbacks of the views do nothing: IsRefreshing is their guard.
    internal sealed class ContainerDebuggerSelection
    {
        public IContainerDiagnostics Container { get; private set; }
        public int Slot { get; private set; } = -1;
        public bool IsRefreshing { get; private set; }

        public void SelectContainer(IContainerDiagnostics container)
        {
            Container = container;
            Slot = -1;
        }

        public void SelectSlot(int slot)
        {
            Slot = slot;
        }

        public void BeginRefresh()
        {
            IsRefreshing = true;
        }

        public void EndRefresh()
        {
            IsRefreshing = false;
        }
    }
}
