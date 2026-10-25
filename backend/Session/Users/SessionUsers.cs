using Internal;
using Shared;

namespace Game.Session;

public interface ISessionUsers : IViewableList<IUser>
{
    int GetNextIndex();
    void AddUser(IUser user);
}

public class SessionUsers : ViewableList<IUser>, ISessionUsers
{
    private int _index;

    // Every new seat (ISessionSeats.Take) takes a new index; a reconnect keeps the index of its seat. Past
    // SharedEntityIds.MaxUserIndex the user would have no entity id range: the join fails (the gateway logs it and closes the socket).
    public int GetNextIndex()
    {
        if (_index >= SharedEntityIds.MaxUserIndex)
        {
            throw new InvalidOperationException(
                $"The session has given out all {SharedEntityIds.MaxUserIndex} user indices: no entity id range is left");
        }

        _index++;
        return _index;
    }

    public void AddUser(IUser user)
    {
        Add(user);
        user.Lifetime.Listen(() => Remove(user));
    }
}

public static class SessionUsersExtensions
{
    extension(ISessionUsers users)
    {
        public void IterateOthers(IUser exclude, Action<IUser> action)
        {
            foreach (var user in users)
            {
                if (user == exclude)
                    continue;

                action(user);
            }
        }

        public async Task IterateOthers(IUser exclude, Func<IUser, Task> action)
        {
            foreach (var user in users)
            {
                if (user == exclude)
                    continue;

                await action(user);
            }
        }

        public void SendAllExceptSelf(IUser self, INetworkContext context)
        {
            foreach (var user in users)
            {
                if (user == self)
                    continue;

                user.Send(context);
            }
        }

        public void SendAll(INetworkContext context)
        {
            foreach (var user in users)
                user.Send(context);
        }
    }
}
