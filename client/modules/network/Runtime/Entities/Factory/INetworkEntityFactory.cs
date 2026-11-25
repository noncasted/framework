using System;
using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public interface INetworkEntityFactory
    {
        INetworkUser LocalUser { get; }

        // Owner of the session entities (ISessionEntityPayload), the same on every client.
        INetworkUser SessionUser { get; }

        INetworkEntityIds Ids { get; }

        // Entities of the other users and of the session, and the ones the backend has built for the local user (their
        // ids are issued by the backend, SharedEntityIds.IsBackendIssued).
        void ListenRemote<T>(
            IReadOnlyLifetime lifetime,
            Func<IReadOnlyLifetime, RemoteEntityData, UniTask<INetworkEntity>> listener);

        // an entity of the local user received from the session (CreatedOverview with the local index as the
        // owner) - the user has returned to its seat. The listener builds the LOCAL variant over the same id
        // (AddLocalEntity with the id + RestoreProperties, no Send); one listener per payload type, an entity without one is an error.
        void ListenRestored<T>(
            IReadOnlyLifetime lifetime,
            Func<IReadOnlyLifetime, RemoteEntityData, UniTask<INetworkEntity>> listener);

        // The local user has returned to its seat (LocalUpdate.IsReturn).
        bool IsReturn { get; }

        // Set by LocalUpdate: whether the user returns and how many of its entities follow it.
        void ExpectRestored(bool isReturn, int count);

        // Completes when every entity of the returned seat has been received (its restore listener is started, not
        // necessarily finished). At once for a user that does not return.
        UniTask WaitRestoredReceived(IReadOnlyLifetime lifetime);

        UniTask Send(IReadOnlyLifetime lifetime, INetworkEntity entity, IEntityPayload payload);

        UniTask CreateRemote(IReadOnlyLifetime lifetime, RemoteEntityData data);

        // A remote entity is destroyed while its listener still builds it: the lifetime the listener got ends.
        // False - no such entity is being built.
        bool TryCancelRemote(int entityId);
    }
}