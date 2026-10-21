using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;

namespace Internal
{
    public class StateMachine : IStateMachine
    {
        private readonly ViewableDelegate _exited = new();
        private readonly Dictionary<IState, UniTaskCompletionSource<bool>> _priorityQueue = new();

        private IReadOnlyLifetime _entityLifetime;

        private IState _currentState;
        private ILifetime _currentLifetime;

        public IViewableDelegate Exited => _exited;
        public IState Current => _currentState;

        public void Initialize(IReadOnlyLifetime lifetime)
        {
            _entityLifetime = lifetime;
            _currentLifetime = null;
            _currentState = null;
        }

        // The machine lives no longer than the entity: after the lifetime terminates (including on exiting
        // Play mode) states must not be entered, the scene objects are already destroyed.
        private bool IsAlive => _entityLifetime != null && _entityLifetime.IsTerminated == false;

        public bool IsAvailable(IState state)
        {
            if (IsAlive == false)
                return false;

            if (_currentState == null)
                return true;

            if (_currentState.Definition == state.Definition)
                return false;

            if (_currentState.Definition.IsTransitable(state.Definition) == false)
                return false;

            return CanInterrupt(_currentState, state);
        }

        /// <summary>
        /// Whether <paramref name="next"/> may interrupt the running <paramref name="current"/>. Always by default:
        /// priorities are resolved only by the queue. A specific object's machine narrows the rule.
        /// </summary>
        protected virtual bool CanInterrupt(IState current, IState next)
        {
            return true;
        }

        public IStateHandle CreateHandle(IState state)
        {
            if (IsAvailable(state) == false)
                throw new InvalidOperationException(
                    $"[StateMachine] CreateHandle: {state.Definition.Name} is not available " +
                    $"(current {_currentState?.Definition.Name ?? "none"}, alive {IsAlive})");

            _currentLifetime?.Terminate();
            _currentState = state;
            _currentLifetime = _entityLifetime.Child();

            var handle = new StateHandle(state, this, _currentLifetime);
            return handle;
        }

        public void Exit(IState state)
        {
            if (state != _currentState)
                throw new InvalidOperationException(
                    $"[StateMachine] Exit: {state.Definition.Name} is not current state ({_currentState?.Definition.Name ?? "none"})");

            _currentLifetime?.Terminate();
            _currentLifetime = null;
            _currentState = null;

            _exited?.Invoke();
        }

        public async UniTask<(bool, IStateHandle)> TryCreateHandleFromPriorityQueue(IState state)
        {
            if (IsAlive == false)
                return (false, null);

            // The queue lives on the main thread only: the entry is added synchronously, no lock is needed.
            var completionSource = new UniTaskCompletionSource<bool>();

            if (_priorityQueue.Count == 0)
                ProcessPriorityQueue().Forget();

            if (_priorityQueue.TryAdd(state, completionSource) == false)
                return (false, null);

            var isSelected = await completionSource.Task;

            if (isSelected == false)
                return (false, null);

            // Waiting for the queue outlives the entity's termination (the continuation arrives a frame later), and
            // while the state was waiting another one could have started, e.g. a key strike entered directly
            // through CreateHandle. The availability check is repeated against what is current now.
            if (IsAvailable(state) == false)
                return (false, null);

            return (true, CreateHandle(state));
        }

        private async UniTask ProcessPriorityQueue()
        {
            await UniTask.Yield(PlayerLoopTiming.Update);

            var selected = IsAlive
                ? _priorityQueue.Keys.OrderByDescending(t => t.Priority).First()
                : null;

            // TrySetResult synchronously continues the waiting state, and that one may immediately
            // queue up again (strike -> signal -> reaction). So first
            // take a snapshot and clear the dictionary, and only then hand out the results.
            var pending = _priorityQueue.ToList();
            _priorityQueue.Clear();

            foreach (var (state, completion) in pending)
                completion.TrySetResult(selected == state);
        }
    }
}