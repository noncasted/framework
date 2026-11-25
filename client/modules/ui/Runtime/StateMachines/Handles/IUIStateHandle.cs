using Cysharp.Threading.Tasks;
using Internal;

namespace Global.UI
{
    public interface IUIStateHandle
    {
        IReadOnlyLifetime InnerLifetime { get; }
        IViewableProperty<bool> IsVisible { get; }

        // The children of the state (CreateChild) that live now. A child does not hide its parent: a parent that
        // closes on a back checks this at the press and leaves the press to the child while there is one. The
        // stacked states are not counted: a state stacked over this one hides it (IsVisible).
        int ChildCount { get; }

        IUIState State { get; }
        UniTaskCompletionSource Completion { get; }

        void Exit();
    }
}