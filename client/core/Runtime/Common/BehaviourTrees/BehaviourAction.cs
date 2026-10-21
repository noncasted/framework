using System;

namespace Internal
{
    // Leaf: the delegate does the work and gives the status, it gets the clock of the tree. onReset is called when
    // the action is dropped while it was Running (interrupted by a parent), not when it finishes by itself.
    public class BehaviourAction : IBehaviourNode
    {
        public BehaviourAction(Func<float, BehaviourStatus> action, Action onReset = null)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _onReset = onReset;
        }

        private readonly Func<float, BehaviourStatus> _action;
        private readonly Action _onReset;

        private bool _isRunning;

        public BehaviourStatus Tick(float time)
        {
            var status = _action.Invoke(time);
            _isRunning = status == BehaviourStatus.Running;
            return status;
        }

        public void Reset()
        {
            if (_isRunning == false)
                return;

            _isRunning = false;
            _onReset?.Invoke();
        }
    }
}
