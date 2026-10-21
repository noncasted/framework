using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Session;

public static class SessionExtensions
{
    // The container of one session. The game adds its own services to it: the network services owned by the backend
    // (IService), its commands (ICommand, IResponseCommand) and its game logic. An ILoggerFactory is registered by
    // the caller (the logs of a session go to the providers of the host).
    public static IServiceCollection AddSessionServices(
        this IServiceCollection services,
        SessionContainerData data,
        SessionCreateOptions options)
    {
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton<ISession, Session>();
        services.AddSingleton<ISessionAdmission, SessionAdmission>();
        services.AddSingleton(options.Time);

        services.AddSingleton<IPropertyUpdateSender, PropertyUpdateSender>();

        services.AddSingleton(data);
        services.AddSingleton(options);

        services.AddSingleton<ISessionData>(new SessionData
        {
            Type = options.Type,
            Id = data.Id,
            Lifetime = data.Lifetime,
            ExpectedUsers = data.ExpectedUsers
        });

        services.AddSingleton<ICommandsCollection, CommandsCollection>();
        services.AddSingleton<IExecutionQueue, ExecutionQueue>();

        services.AddSingleton<IUserFactory, UserFactory>();
        services.AddSingleton<ISessionUsers, SessionUsers>();
        services.AddSingleton<ISessionSeats, SessionSeats>();
        services.AddSingleton<ISessionOwner, SessionOwner>();

        services.AddSingleton<ISessionObjects, SessionObjects>();
        services.AddSingleton<IEntityFactory, EntityFactory>();
        services.AddSingleton<ISessionEntities, SessionEntities>();

        services.Add<IServiceFactory, ServiceFactory>();

        services.AddSingleton<ISessionServices, SessionServices>();

        services.AddSingleton<IResponseCommand, EntityCreateCommand>();
        services.AddSingleton<ICommand, SetPropertyCommand>();
        services.AddSingleton<ICommand, SetPropertiesCommand>();
        services.AddSingleton<ICommand, EntityDestroyCommand>();
        services.AddSingleton<ICommand, EntityEventCommand>();
        services.AddSingleton<IResponseCommand, ServiceGetOrCreateCommand>();

        services.AddSingleton<ICommand, LeaveCommand>();
        services.AddSingleton<ICommand, PingCommand>();
        services.AddSingleton<ICommand, PeerProbeCommand>();
        services.AddSingleton<ICommand, PeerEchoCommand>();

        return services;
    }
}
