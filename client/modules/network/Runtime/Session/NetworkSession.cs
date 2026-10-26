using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    public interface INetworkSession
    {
        INetworkUsersCollection Users { get; }
        IReadOnlyLifetime Lifetime { get; }

        // The ticket (from the meta: LobbyResult.Ticket or IMatchmaking.GetSessionTicket) names the user and the
        // session; it is accepted once, within a minute of its issue.
        UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, string ticket);
    }

    public class NetworkSession : INetworkSession
    {
        public NetworkSession(
            INetworkSessionCallbacks callbacks,
            INetworkUsersCollection users,
            ISessionConnection connection,
            INetworkConnection networkConnection)
        {
            _callbacks = callbacks;
            _connection = connection;
            _networkConnection = networkConnection;
            Users = users;
        }

        private readonly INetworkSessionCallbacks _callbacks;
        private readonly ISessionConnection _connection;
        private readonly INetworkConnection _networkConnection;

        // As the timeout of a request: the local user follows the response to the authorization.
        private static readonly TimeSpan LocalUserTimeout = TimeSpan.FromSeconds(20);

        private ILifetime _lifetime;

        public INetworkUsersCollection Users { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;

        public async UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, string ticket)
        {
            _lifetime = lifetime.Child();

            Debug.Log("[Network] [Session] Starting session connection...");
            await _connection.Connect(_lifetime, serverUrl, ticket);
            Debug.Log("[Network] [Session] Session connection established.");
            Debug.Log("[Network] [Session] Waiting for local user data...");
            await WaitLocalUser();

            Debug.Log("[Network] [Session] Local user data received.");
            Debug.Log("[Network] [Session] Starting session services");
            await _callbacks.InvokeSessionSetupCompleted(_lifetime);
        }

        // The server sends the local user right after the authorization. A connection lost before it never brings
        // it, and a server that does not send it is a failed entry, not an endless wait.
        private async UniTask WaitLocalUser()
        {
            await UniTask
                  .WaitUntil(() => Users.Local != null || _networkConnection.Writer.IsClosed == true,
                      cancellationToken: _lifetime.Token)
                  .Timeout(LocalUserTimeout, DelayType.Realtime);

            if (Users.Local == null)
                throw new OperationCanceledException("[Network] [Session] The connection is lost before the local user data");
        }
    }

    public static class SessionContextExtensions
    {
        public static int LocalIndex(this INetworkSession context)
        {
            return context.Users.Local.Index;
        }
    }
}