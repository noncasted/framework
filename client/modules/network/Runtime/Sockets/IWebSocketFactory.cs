namespace Internal
{
    // The transport of a connection by its url and the platform.
    public interface IWebSocketFactory
    {
        IWebSocket Create(string url, IReadOnlyLifetime lifetime);
    }
}
