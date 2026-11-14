namespace Common.Network;

// The HMAC key of SessionTickets, shared by the meta and the game gateways (SESSION_TICKET_SECRET,
// base64, at least 32 bytes). Read by Orchestration.SessionTicketsExtensions.
public sealed class SessionTicketKey
{
    public SessionTicketKey(byte[] value)
    {
        if (value.Length < MinLength)
            throw new ArgumentException($"A session ticket key needs at least {MinLength} bytes, got {value.Length}", nameof(value));

        Value = value;
    }

    public const int MinLength = 32;

    public byte[] Value { get; }
}
