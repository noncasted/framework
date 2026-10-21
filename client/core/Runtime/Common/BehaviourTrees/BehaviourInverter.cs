using System;

namespace Internal
{
    // Decorator: Success <-> Failure, Running stays Running.
    public class BehaviourInverter : IBehaviourNode
    {
        public BehaviourInverter(IBehaviourNode child)
        {
            _child = child ?? throw new ArgumentNullException(nameof(child));
        }

        private readonly IBehaviourNode _child;

        public BehaviourStatus Tick(float time)
        {
            return _child.Tick(time) switch
            {
                BehaviourStatus.Success => BehaviourStatus.Failure,
                BehaviourStatus.Failure => BehaviourStatus.Success,
                _ => BehaviourStatus.Running
            };
        }

        public void Reset()
        {
            _child.Reset();
        }
    }
}
