using System;

namespace Internal
{
    // Decorator: a child that is Running for duration seconds of the tree clock is reset and the node fails. The time
    // is counted from the first tick of a run.
    public class BehaviourTimeout : IBehaviourNode
    {
        public BehaviourTimeout(float duration, IBehaviourNode child)
        {
            if (duration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Must be positive");

            _duration = duration;
            _child = child ?? throw new ArgumentNullException(nameof(child));
        }

        private readonly float _duration;
        private readonly IBehaviourNode _child;

        private float _startTime;
        private bool _isRunning;

        public BehaviourStatus Tick(float time)
        {
            if (_isRunning == false)
                _startTime = time;

            if (_isRunning == true && time - _startTime >= _duration)
            {
                _isRunning = false;
                _child.Reset();
                return BehaviourStatus.Failure;
            }

            var status = _child.Tick(time);
            _isRunning = status == BehaviourStatus.Running;
            return status;
        }

        public void Reset()
        {
            if (_isRunning == false)
                return;

            _isRunning = false;
            _child.Reset();
        }
    }
}
