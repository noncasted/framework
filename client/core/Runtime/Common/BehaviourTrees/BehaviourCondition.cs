using System;

namespace Internal
{
    // Leaf: Success when the check is true, Failure otherwise. Never Running.
    public class BehaviourCondition : IBehaviourNode
    {
        public BehaviourCondition(Func<bool> check)
        {
            _check = check ?? throw new ArgumentNullException(nameof(check));
        }

        private readonly Func<bool> _check;

        public BehaviourStatus Tick(float time)
        {
            return _check.Invoke() == true ? BehaviourStatus.Success : BehaviourStatus.Failure;
        }

        public void Reset()
        {
        }
    }
}
