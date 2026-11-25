namespace Global.Publisher
{
    // What the platform says about the user. Ticket: nothing for a guest (the game has its own id and secret), the signed
    // initData for Telegram, the hex ticket of the Web API for Steam. Name: the name the user has in the platform, if any.
    public readonly struct PlatformCredentials
    {
        public PlatformCredentials(PlatformAuthType type, string ticket, string name)
        {
            Type = type;
            Ticket = ticket;
            Name = name;
        }

        public PlatformAuthType Type { get; }
        public string Ticket { get; }
        public string Name { get; }
    }
}
