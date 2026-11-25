namespace Internal
{
    // The protocol hash of the project (its shared ProtocolHash.Value), sent in the query of every socket upgrade.
    public sealed class NetworkProtocolHash
    {
        public NetworkProtocolHash(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }
}
