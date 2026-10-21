namespace Internal
{
    // Node of a behaviour tree. A tree is built once by code (BehaviourNodes), its delegates are created at the build:
    // a tick allocates nothing.
    public interface IBehaviourNode
    {
        // time - the clock of the tree in seconds (BehaviourTree.Time), it never goes back.
        BehaviourStatus Tick(float time);

        // The work of the node is dropped: a Running node was interrupted by its parent or the whole tree starts
        // over. What the node remembers between its runs (the timer of a cooldown) stays.
        void Reset();
    }
}
