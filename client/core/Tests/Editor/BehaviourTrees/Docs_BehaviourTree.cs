using static Internal.BehaviourNodes;

namespace Internal.Tests
{
    // Example for .claude/docs/BEHAVIOUR_TREES.md: a guard that eats when hungry, chases what it sees and patrols
    // otherwise. The tree decides, somebody else acts: the leaves only write the intention (here - Intent), the way a
    // real owner asks its state machine for a state.
    public class Docs_BehaviourTree
    {
        public Docs_BehaviourTree()
        {
            // Built once. The lambdas capture this: nothing is allocated in a tick.
            _tree = new BehaviourTree(Selector(
                // 1. A condition and what to do under it. The eating is Running for a while: the sequence remembers
                //    it and doesn't ask IsHungry again until it is done. Not more often than once in 10 seconds.
                Sequence(
                    Condition(() => Hunger > 0.8f),
                    Cooldown(10f, Action(Eat))),

                // 2. The selector asks its children from the first one on every tick: when the guard gets hungry in
                //    the middle of a chase, branch 1 wins and the chase below is reset (onReset of its action).
                Sequence(
                    Condition(() => SeesIntruder),
                    Timeout(5f, Action(Chase, () => Intent = "lost"))),

                // 3. The last child is the default: it never fails.
                Action(_ => {
                    Intent = "patrol";
                    return BehaviourStatus.Success;
                })));
        }

        private readonly BehaviourTree _tree;

        private float _eatEndTime = -1f;

        public float Hunger { get; private set; }
        public bool SeesIntruder { get; private set; }
        public string Intent { get; private set; } = "none";

        // What the owner has seen since the previous Think.
        public void Sense(float hunger, bool seesIntruder)
        {
            Hunger = hunger;
            SeesIntruder = seesIntruder;
        }

        // Called by the owner on its own interval (an enemy - every 0.1 s of the game time), delta - the time since
        // the previous call.
        public void Think(float delta)
        {
            _tree.Tick(delta);
        }

        // The owner lost control (died, was stunned): whatever was Running is dropped, cooldowns keep counting.
        public void Interrupt()
        {
            _tree.Reset();
        }

        // time - the clock of the tree: the action measures its own duration by it.
        private BehaviourStatus Eat(float time)
        {
            if (_eatEndTime < 0f)
                _eatEndTime = time + 2f;

            Intent = "eat";

            if (time < _eatEndTime)
                return BehaviourStatus.Running;

            _eatEndTime = -1f;
            Hunger = 0f;
            return BehaviourStatus.Success;
        }

        private BehaviourStatus Chase(float time)
        {
            Intent = "chase";
            return BehaviourStatus.Running;
        }
    }
}
