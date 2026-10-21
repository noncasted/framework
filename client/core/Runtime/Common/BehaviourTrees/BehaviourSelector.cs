using System;

namespace Internal
{
    // Priority choice: every tick the children are asked from the first one, the first that doesn't fail gives the
    // result. A Running child of the previous tick that lost to a child before it (or failed on its own turn) is
    // reset: the higher branch interrupts the lower one.
    public class BehaviourSelector : IBehaviourNode
    {
        public BehaviourSelector(IBehaviourNode[] children)
        {
            if (children == null || children.Length == 0)
                throw new ArgumentException("A selector needs children", nameof(children));

            _children = children;
        }

        private const int None = -1;

        private readonly IBehaviourNode[] _children;

        private int _running = None;

        public BehaviourStatus Tick(float time)
        {
            for (var i = 0; i < _children.Length; i++)
            {
                var status = _children[i].Tick(time);

                if (status == BehaviourStatus.Failure)
                    continue;

                if (_running != None && _running != i)
                    _children[_running].Reset();

                _running = status == BehaviourStatus.Running ? i : None;
                return status;
            }

            // Every child failed, the one that was Running too: it has finished by itself.
            _running = None;
            return BehaviourStatus.Failure;
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
