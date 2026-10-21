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

        private readonly IBehaviourNode[] _children;

        private int _current;

        public BehaviourStatus Tick(float time)
        {
            for (var i = _current; i < _children.Length; i++)
            {
                var status = _children[i].Tick(time);

                if (status == BehaviourStatus.Success)
                    continue;

                _current = status == BehaviourStatus.Running ? i : 0;
                return status;
            }

            _current = 0;
            return BehaviourStatus.Success;
        }

        public void Reset()
        {
            if (_current == 0)
                return;

            _children[_current].Reset();
            _current = 0;
        }
    }
}
