using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    // State machine of a remote copy: it does not pick states itself but enters the one requested
    // by the owner's local state. Each entry ends the lifetime of the previous state.
    public class RemoteStateMachine : IRemoteStateMachine
    {
        public RemoteStateMachine(IReadOnlyList<IRemoteState> states)
        {
            _states = states;
        }

        private readonly IReadOnlyList<IRemoteState> _states;

        private IReadOnlyLifetime _entityLifetime;

        private IRemoteState _currentState;
        private ILifetime _currentLifetime;

        public IRemoteState Current => _currentState;

        public void Initialize(IReadOnlyLifetime lifetime)
        {
            _entityLifetime = lifetime;
            _currentLifetime = null;
            _currentState = null;
        }

        public void Enter(IStateDefinition definition, IStatePayload payload)
        {
            var state = Find(definition);

            _currentLifetime?.Terminate();
            _currentState = state;
            _currentLifetime = _entityLifetime.Child();

            state.Enter(_currentLifetime, payload).NoAwait();
        }

        private IRemoteState Find(IStateDefinition definition)
        {
            foreach (var state in _states)
            {
                if (state.Definition == definition)
                    return state;
            }

            throw new InvalidOperationException($"[RemoteStateMachine] No remote state for {definition.Name}");
        }
    }
}
