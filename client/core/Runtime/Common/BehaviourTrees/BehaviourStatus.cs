namespace Internal
{
    public enum BehaviourStatus
    {
        Success,
        Failure,

        // The node is not done: it is ticked again on the next tick of the tree.
        Running
    }
}
