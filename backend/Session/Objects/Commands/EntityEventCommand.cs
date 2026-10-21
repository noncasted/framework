using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Session;

// Forwards an event without reading it: of an entity - to every other user, of a service - to its other subscribers
// (a client that has not fetched the service yet doesn't know its id). An object that is gone (destroyed, its owner
// left) - dropped, a race; an id that never existed in the session - the command fails (CommandDispatcher logs it).
public class EntityEventCommand : Command<SharedSessionObject.Event>
{
    public EntityEventCommand(ISessionObjects objects, ISessionUsers users, ILogger<EntityEventCommand> logger)
    {
        _objects = objects;
        _users = users;
        _logger = logger;
    }

    private readonly ISessionObjects _objects;
    private readonly ISessionUsers _users;
    private readonly ILogger<EntityEventCommand> _logger;

    protected override void Execute(IUser user, SharedSessionObject.Event context)
    {
        if (_objects.Entries.TryGetValue(context.ObjectId, out var networkObject) == false)
        {
            if (_objects.IsRemoved(context.ObjectId) == true)
            {
                _logger.LogDebug("[Session] User {Index} sends an event to destroyed object {ObjectId}: dropped",
                    user.Index,
                    context.ObjectId);

                return;
            }

            throw new InvalidOperationException(
                $"User {user.Index} sends an event to object {context.ObjectId} that does not exist in the session");
        }

        if (networkObject is IService service)
        {
            foreach (var subscriber in service.Subscribers)
            {
                if (subscriber != user)
                    subscriber.Send(context);
            }

            return;
        }

        _users.SendAllExceptSelf(user, context);
    }
}
