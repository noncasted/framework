namespace Internal
{
    // The properties written since the last sync, in the order of their first write.
    public interface INetworkDirtyProperties
    {
        void OnDirty(INetworkObject owner, INetworkProperty property);
    }
}
