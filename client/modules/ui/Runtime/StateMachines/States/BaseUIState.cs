using Cysharp.Threading.Tasks;
using Internal;

namespace Global.UI
{
    public class BaseUIState : IInternalUIStateHandle, IUIState
    {
        public BaseUIState(IReadOnlyLifetime parent)
        {
            OuterLifetime = parent.Child();
            InnerLifetime = parent.Child();
            Recovered = new ViewableDelegate();
        }

        private int _childCount;

        public IUIConstraints Constraints => new UIConstraints();

        public IReadOnlyLifetime InnerLifetime { get; }
        public IReadOnlyLifetime OuterLifetime { get; }
        public IViewableProperty<bool> IsVisible { get; } = new ViewableProperty<bool>(true);
        public int ChildCount => _childCount;
        public IViewableDelegate Recovered { get; }
        public IUIState State => this;
        public UniTaskCompletionSource Completion { get; } = new();

        public void Exit()
        {
        }

        public void OnStacked(IInternalUIStateHandle stackHead)
        {

        }

        public void OnParent(IInternalUIStateHandle child)
        {
            _childCount++;
            child.InnerLifetime.Listen(() => _childCount--);
        }

        public void ClearStack()
        {
        }
    }
}