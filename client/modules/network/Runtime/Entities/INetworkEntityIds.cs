namespace Internal
{
    public interface INetworkEntityIds
    {
        int GetEntityId();

        // the local user has returned to its seat, its range is used up to the counter (LocalUpdate): the next
        // ids go on after it. Only before the first id of this session.
        void Continue(int counter);
    }
}
