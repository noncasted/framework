using System;
using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public static class NetworkEntityFactoryExtensions
    {
        public static UniTask Send<T>(
            this INetworkEntityFactory factory,
            IReadOnlyLifetime lifetime,
            INetworkEntity entity)
            where T : IEntityPayload, new()
        {
            return factory.Send(lifetime, entity, new T());
        }

        public static IEntityBuilder AddLocalEntity(this IEntityBuilder builder, INetworkEntityFactory factory)
        {
            builder.Register<NetworkEntity>()
                   .WithParameter(factory.LocalUser)
                   .WithParameter(factory.Ids.GetEntityId())
                   .As<INetworkEntity>();

            return builder;
        }

        // the same local entity over an id given before: a new one (Ids.GetEntityId) or the id of an entity of
        // the returned seat (ListenRestored, then RestoreProperties instead of Send).
        public static IEntityBuilder AddLocalEntity(this IEntityBuilder builder, INetworkEntityFactory factory, int entityId)
        {
            builder.Register<NetworkEntity>()
                   .WithParameter(factory.LocalUser)
                   .WithParameter(entityId)
                   .As<INetworkEntity>();

            return builder;
        }

        // A session entity created by this client: owned by the session like its received copies on the other clients
        // (the backend keeps it until the session ends), so the creator leaving doesn't change it.
        public static IEntityBuilder AddSessionEntity(this IEntityBuilder builder, INetworkEntityFactory factory)
        {
            builder.Register<NetworkEntity>()
                   .WithParameter(factory.SessionUser)
                   .WithParameter(factory.Ids.GetEntityId())
                   .As<INetworkEntity>();

            return builder;
        }

        public static IEntityBuilder AddRemoteEntity(this IEntityBuilder builder, RemoteEntityData data)
        {
            builder.Register<NetworkEntity>()
                   .WithParameter(data.Owner)
                   .WithParameter(data.Id)
                   .As<INetworkEntity>();

            return builder;
        }

        // the values of the session replace whatever the local components have set on their setup, and are
        // not sent back (the session has them already): the properties are not dirty after it.
        public static IEntityScopeResult RestoreProperties(this IEntityScopeResult result, RemoteEntityData data)
        {
            if (data.Owner.IsLocal == false)
                throw new InvalidOperationException($"[Network] Entity {data.Id} of user {data.Owner.Index} is restored as a local one");

            if (result.Get<INetworkEntity>().Id != data.Id)
                throw new InvalidOperationException($"[Network] Entity {result.Get<INetworkEntity>().Id} is restored from entity {data.Id}");

            result.FillProperties(data);

            foreach (var (_, property) in result.Get<INetworkEntity>().Properties)
                property.Collect();

            return result;
        }

        public static IEntityScopeResult FillProperties(this IEntityScopeResult result, RemoteEntityData data)
        {
            var properties = result.Get<INetworkEntity>().Properties;

            if (properties.Count != data.RawProperties.Count)
                throw new InvalidOperationException(
                    $"Properties count mismatch local: {properties.Count} != remote: {data.RawProperties.Count}");

            foreach (var rawProperty in data.RawProperties)
            {
                // The version of the value on the backend (CreatedOverview): a later PropertyUpdate applies only
                // if it is newer.
                if (properties.TryGetValue(rawProperty.PropertyId, out var property) == false)
                {
                    throw new InvalidOperationException(
                        $"Entity {data.Id} received property {rawProperty.PropertyId} that its local copy does not have");
                }

                property.Update(rawProperty.Value, rawProperty.Version);
            }

            return result;
        }
    }
}
