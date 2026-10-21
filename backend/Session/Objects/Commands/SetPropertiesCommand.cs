using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Session;

// The writes of one sync of a client (NetworkPropertiesCollector). Each entry is handled as its own SetProperty: a
// failed one is logged like a failed one-way command, the others still apply.
public class SetPropertiesCommand : Command<SharedSessionObject.SetProperties>
{
    public SetPropertiesCommand(
        ISessionObjects objects,
        ISessionUsers users,
        ILogger<SetPropertyCommand> singleLogger,
        ILogger<SetPropertiesCommand> logger)
    {
        _single = new SetPropertyCommand(objects, users, singleLogger);
        _logger = logger;
    }

    private readonly ICommand _single;
    private readonly ILogger<SetPropertiesCommand> _logger;

    protected override void Execute(IUser user, SharedSessionObject.SetProperties context)
    {
        foreach (var entry in context.Entries)
        {
            try
            {
                _single.Execute(user, entry);
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "[Session] [Command] Write of property {PropertyId} of object {ObjectId} by user {Index} failed",
                    entry.PropertyId,
                    entry.ObjectId,
                    user.Index);
            }
        }
    }
}
