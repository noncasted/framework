using System;
using Cysharp.Threading.Tasks;
using Global.Inputs;
using Internal;

namespace Global.UI
{
    public class UIStateHandle : IInternalUIStateHandle
    {
        public UIStateHandle(
            IReadOnlyLifetime lifetime,
            IInternalUIStateHandle parent,
            IUIState state,
            IInputConstraintsStorage constraintsStorage)
        {
            _parent = parent;
            State = state;
            Completion = new UniTaskCompletionSource();

            // The handle lives no longer than its parent state and its owner: the machine and its base state are
            // global, so a handle bound only to the parent would outlive the scope that opened it.
            _innerLifetime = parent.InnerLifetime.Child();
            Action terminate = _innerLifetime.Terminate;
            lifetime.Listen(terminate);
            _innerLifetime.Listen(() => lifetime.RemoveListener(terminate));
            _outerLifetime = _innerLifetime.Child();

            _isVisible.View(_innerLifetime, isVisible => {
                if (isVisible == true)
                    constraintsStorage.Add(State.Constraints);
                else
                    constraintsStorage.Remove(State.Constraints);
            });

            _innerLifetime.Listen(() => {
                if (_isVisible.Value == true)
                    constraintsStorage.Remove(State.Constraints);

                Completion.TrySetResult();
            });
        }

        private readonly IInternalUIStateHandle _parent;
        private readonly ViewableProperty<bool> _isVisible = new(true);

        private readonly ILifetime _innerLifetime;
        private ILifetime _outerLifetime;
        private int _childCount;
        private bool _isParentVisible = true;
        private int _coveringStacks;

        public IReadOnlyLifetime InnerLifetime => _innerLifetime;
        public IReadOnlyLifetime OuterLifetime => _outerLifetime;
        public IViewableProperty<bool> IsVisible => _isVisible;
        public int ChildCount => _childCount;
        public IUIState State { get; }
        public UniTaskCompletionSource Completion { get; }

        // Visible while the parent is (a child, OnChild) and no stacked state covers it: one flag written by both
        // showed a state under a dialog when its parent was shown again, and the end of one of two stacked states
        // showed it under the other.
        public void OnStacked(IInternalUIStateHandle stackHead)
        {
            _coveringStacks++;
            UpdateVisible();

            stackHead.InnerLifetime.Listen(() => {
                _coveringStacks--;
                UpdateVisible();
            });
        }

        public void OnChild()
        {
            _parent.IsVisible.View(InnerLifetime, isVisible => {
                _isParentVisible = isVisible;
                UpdateVisible();
            });
        }

        private void UpdateVisible()
        {
            if (_innerLifetime.IsTerminated == true)
                return;

            _isVisible.Set(_isParentVisible == true && _coveringStacks == 0);
        }

        public void OnParent(IInternalUIStateHandle child)
        {
            _childCount++;
            child.InnerLifetime.Listen(() => _childCount--);
        }

        public void ClearStack()
        {
            _outerLifetime.Terminate();
            _outerLifetime = _innerLifetime.Child();
        }

        public void Exit()
        {
            _isVisible.Set(false);
            _innerLifetime.Terminate();
        }
    }
}