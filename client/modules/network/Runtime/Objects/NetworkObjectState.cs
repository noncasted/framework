namespace Internal
{
    // What this client knows about an object id of the session.
    public enum NetworkObjectState
    {
        // Never seen: a message for it is a bug of the protocol (an exception).
        Unknown,

        // CreatedOverview received, the listener still builds the object: its updates and events wait for it.
        Creating,

        Live,

        // Was here and is gone (destroyed, its owner left, its creation failed). Ids are not reused in a session,
        // so a message for it is a race with the destroy and is dropped.
        Destroyed
    }
}
