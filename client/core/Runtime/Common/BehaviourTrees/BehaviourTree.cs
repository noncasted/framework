using System;

namespace Internal
{
    // Root of a behaviour tree and its clock. The owner ticks it with the time passed since its previous tick: the
    // interval is up to the owner (not every frame, see .claude/docs/BEHAVIOUR_TREES.md).
    public class BehaviourTree
    {
        public BehaviourTree(IBehaviourNode root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        private readonly IBehaviourNode _root;

        // Seconds the tree has lived: the sum of the deltas of its ticks.
        public float Time { get; private set; }

        public BehaviourStatus Status { get; private set; } = BehaviourStatus.Failure;

        public BehaviourStatus Tick(float delta)
        {
            if (delta < 0f)
                throw new ArgumentOutOfRangeException(nameof(delta), delta, "The clock of a tree never goes back");

            Time += delta;
            Status = _root.Tick(Time);
            return Status;
        }

        // Interrupts whatever is Running. The clock goes on: cooldowns keep counting.
        public void Reset()
        {
            _root.Reset();
            Status = BehaviourStatus.Failure;
        }
    }
}
