namespace Internal
{
    public interface ILocalServerConnection
    {
        void OnReceived(byte[] bytes);
    }
}
