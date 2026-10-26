using Internal;
using Shared;

namespace Game.Session;

public interface IServiceFactory
{
    // Any thread (the factory of the game calls it right after the session is built): the services of the container
    // are set up in the session queue, where the users, the owner and the objects they subscribe to live. Completes
    // when they are.
    Task OnSessionCreated(IReadOnlyLifetime lifetime);

    // Runs in the session queue.
    IService GetOrCreate(SharedSessionService.GetRequest request);
}

public class ServiceFactory : IServiceFactory
{
    public ServiceFactory(
        IEnumerable<IService> services,
        ISessionServices collection,
        ISessionData data,
        ISessionObjects objects,
        IExecutionQueue executionQueue)
    {
        _services = services;
        _collection = collection;
        _data = data;
        _objects = objects;
        _executionQueue = executionQueue;
    }

    private readonly IEnumerable<IService> _services;
    private readonly ISessionServices _collection;
    private readonly ISessionData _data;
    private readonly ISessionObjects _objects;
    private readonly IExecutionQueue _executionQueue;

    public Task OnSessionCreated(IReadOnlyLifetime lifetime)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _executionQueue.Enqueue(() => {
            try
            {
                foreach (var service in _services)
                {
                    service.Setup(_data.Lifetime);
                    Register(service);
                }

                completion.SetResult();
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        });

        return completion.Task;
    }

    public IService GetOrCreate(SharedSessionService.GetRequest request)
    {
        if (_collection.Entries.TryGetValue(request.Key, out var existing))
            return existing;

        var service = new Service(request.Key);

        foreach (var propertyId in request.PropertiesIds)
            service.BindProperty(new ObjectProperty(propertyId, []));

        service.Setup(_data.Lifetime);
        Register(service);

        return service;
    }

    // Both or none: the objects refuse an id taken by another key (Service.GetId) before the key is registered, so a
    // refused service is not found by a later request.
    private void Register(IService service)
    {
        if (_collection.Entries.ContainsKey(service.Key) == true)
            throw new InvalidOperationException($"Service {service.Key} is already registered");

        _objects.Add(service);
        _collection.Add(service);
    }
}
