namespace Internal
{
    // A connect stopped because the client is outdated (IClientOutdatedNotice.IsShown): retrying it cannot succeed.
    public class ClientOutdatedException : System.Exception
    {
        public ClientOutdatedException(string message) : base(message)
        {
        }
    }
}
