namespace Internal
{
    public static class SessionContextExtensions
    {
        public static int LocalIndex(this INetworkSession context)
        {
            return context.Users.Local.Index;
        }
    }
}
