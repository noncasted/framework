using System;

namespace Internal
{
    // Steps in order: succeeds when every child succeeded, fails with the first failed child. Remembers the Running
    // child: the next tick goes on from it, the children before it are not asked again.
    public class BehaviourSequence : IBehaviourNode
    {
        public BehaviourSequence(IBehaviourNode[] children)
        {
            if (children == null || children.Length == 0)
                throw new ArgumentException("A sequence needs children", nameof(children));

            _children = children;
        }

        // No Running child: index 0 would mean both "idle" and "the first child is Running".
        private const int None = -1;

        private readonly IBehaviourNode[] _children;

        private int _running = None;

        public BehaviourStatus Tick(float time)
        {
            var first = _running == None ? 0 : _running;

            for (var i = first; i < _children.Length; i++)
            {
                var status = _children[i].Tick(time);

                if (status == BehaviourStatus.Success)
                    continue;

                _running = status == BehaviourStatus.Running ? i : None;
                return status;
            }

            _running = None;
            return BehaviourStatus.Success;
        }

        public void Reset()
        {
            if (_running == None)
                return;

            _children[_running].Reset();
            _running = None;
        }
    }
}
