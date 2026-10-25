using Internal;
using Shared;

namespace Game.Session;

public interface ISessionData
{
    SessionType Type { get; }
    int ExpectedUsers { get; }
    Guid Id { get; }
    IReadOnlyLifetime Lifetime { get; }
}

public class SessionData : ISessionData
{
    public required SessionType Type { get; init; }
    public required int ExpectedUsers { get; init; }
    public required Guid Id { get; init; }
    public required IReadOnlyLifetime Lifetime { get; init; }
}
