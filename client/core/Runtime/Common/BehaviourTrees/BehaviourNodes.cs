using System;

namespace Internal
{
    // Short names for building a tree by code:
    // using static Internal.BehaviourNodes;
    // var tree = new BehaviourTree(Selector(Sequence(Condition(IsHungry), Action(Eat)), Action(Sleep)));
    public static class BehaviourNodes
    {
        public static IBehaviourNode Selector(params IBehaviourNode[] children)
        {
            return new BehaviourSelector(children);
        }

        public static IBehaviourNode Sequence(params IBehaviourNode[] children)
        {
            return new BehaviourSequence(children);
        }

        public static IBehaviourNode Condition(Func<bool> check)
        {
            return new BehaviourCondition(check);
        }

        public static IBehaviourNode Action(Func<float, BehaviourStatus> action, Action onReset = null)
        {
            return new BehaviourAction(action, onReset);
        }

        public static IBehaviourNode Inverter(IBehaviourNode child)
        {
            return new BehaviourInverter(child);
        }

        public static IBehaviourNode Cooldown(float duration, IBehaviourNode child, float delay = 0f)
        {
            return new BehaviourCooldown(duration, child, delay);
        }

        public static IBehaviourNode Timeout(float duration, IBehaviourNode child)
        {
            return new BehaviourTimeout(duration, child);
        }
    }
}
