using System;

namespace Internal
{
    // Decorator: after the child is done (Success or Failure, or it was interrupted while Running) it is not asked
    // for the next duration seconds of the tree clock - the node fails without ticking it. A Running child holds the
    // node open. The timer outlives Reset. delay - seconds before the first run.
    public class BehaviourCooldown : IBehaviourNode
    {
        public BehaviourCooldown(float duration, IBehaviourNode child, float delay = 0f)
        {
            if (duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Must not be negative");

            _duration = duration;
            _child = child ?? throw new ArgumentNullException(nameof(child));
            _readyTime = delay;
        }

        private readonly float _duration;
        private readonly IBehaviourNode _child;

        private float _readyTime;
        private float _time;
        private bool _isRunning;

        public BehaviourStatus Tick(float time)
        {
            _time = time;

            if (_isRunning == false && time < _readyTime)
                return BehaviourStatus.Failure;

            var status = _child.Tick(time);
            _isRunning = status == BehaviourStatus.Running;

            if (_isRunning == false)
                _readyTime = time + _duration;

            return status;
        }

        public void Reset()
        {
            if (_isRunning == false)
                return;

            _isRunning = false;
            _readyTime = _time + _duration;
            _child.Reset();
        }
    }
}
