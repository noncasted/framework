namespace Global.UI
{
    public interface IInternalUIStateHandle : IUIStateHandle
    {
        void OnStacked(IInternalUIStateHandle stackHead);

        // A child (CreateChild) of this state is created: it counts in ChildCount while it lives.
        void OnParent(IInternalUIStateHandle child);

    }
}