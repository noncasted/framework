using System.Collections.Generic;
using Global.Inputs;
using Internal;

namespace Global.UI
{
    public class UIStateMachine : IUIStateMachine
    {
        public UIStateMachine(IInputConstraintsStorage constraintsStorage, IReadOnlyLifetime lifetime)
        {
            _constraintsStorage = constraintsStorage;

            var state = new BaseUIState(lifetime);
            Base = state;

            // Nothing is stacked yet: the head is the base, so a window opened over the head (CreateChild with
            // StackHead) has a parent before the first stack too.
            _stackHead = state;

            _handles = new Dictionary<IUIState, IInternalUIStateHandle>()
            {
                { state, state }
            };
        }

        private IUIState _stackHead;

        // The stacked states that live, in the order they were stacked.
        private readonly List<UIStateHandle> _stack = new();

        private readonly IInputConstraintsStorage _constraintsStorage;
        private readonly Dictionary<IUIState, IInternalUIStateHandle> _handles;

        public IUIState Base { get; }
        public IUIState StackHead => _stackHead;

        public IUIStateHandle CreateChild(IReadOnlyLifetime lifetime, IUIState parent, IUIState state)
        {
            var headHandle = _handles[parent];
            var childHandle = new UIStateHandle(lifetime, headHandle, state, _constraintsStorage);
            Add(state, childHandle);
            childHandle.OnChild();
            headHandle.OnParent(childHandle);

            return childHandle;
        }

        public IUIStateHandle CreateStackChild(IReadOnlyLifetime lifetime, IUIState parent, IUIState state)
        {
            var headHandle = _handles[parent];
            var childHandle = new UIStateHandle(lifetime, headHandle, state, _constraintsStorage);
            Add(state, childHandle);
            headHandle.OnStacked(childHandle);

            // The head is the newest stacked state that lives: the stacked states close in any order (one under
            // another first, with its stacked children), and a head restored to the one captured at the start was
            // a state already gone.
            _stack.Add(childHandle);
            _stackHead = state;

            childHandle.InnerLifetime.Listen(() => {
                _stack.Remove(childHandle);
                _stackHead = _stack.Count > 0 ? _stack[_stack.Count - 1].State : Base;
            });

            return childHandle;
        }

        public void Exit(IUIState state)
        {
            _handles[state].Exit();
        }

        // The handle is known while it lives: a state entered again (the same window reopened) replaces it, and the
        // end of the old one does not remove the new one.
        private void Add(IUIState state, UIStateHandle handle)
        {
            _handles[state] = handle;

            handle.InnerLifetime.Listen(() => {
                if (_handles.TryGetValue(state, out var current) == true && current == handle)
                    _handles.Remove(state);
            });
        }
    }
}