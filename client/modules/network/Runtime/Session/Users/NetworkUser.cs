using System;
using System.Collections.Generic;

namespace Internal
{
    public interface INetworkUser
    {
        int Index { get; }

        // The id of the user in this session (SharedSessionPlayer.LocalUpdate / RemoteUpdate): the meta id of a user
        // is its login and never reaches the other clients. Empty for the session "user".
        Guid Id { get; }

        bool IsLocal { get; }
        IReadOnlyDictionary<int, INetworkEntity> Entities { get; }
        IReadOnlyLifetime Lifetime { get; }

        // The connection of this remote user is lost, its seat (and its entities) wait for its return
        // (SharedSessionPlayer.RemoteAway). Nobody writes its entities meanwhile: it takes no part in the rules
        // of the match. Always false for the local user and the session.
        IViewableProperty<bool> IsAway { get; }

        void AddEntity(INetworkEntity entity);
        void SetAway(bool isAway);
        void DisposeRemote();
    }

    public class NetworkUser : INetworkUser
    {
        public NetworkUser(
            int index,
            Guid id,
            bool isLocal,
            ILifetime lifetime)
        {
            _lifetime = lifetime;
            Index = index;
            Id = id;
            IsLocal = isLocal;
        }

        private readonly ILifetime _lifetime;
        private readonly Dictionary<int, INetworkEntity> _entities = new();
        private readonly ViewableProperty<bool> _isAway = new(false);

        public int Index { get; }
        public Guid Id { get; }
        public bool IsLocal { get; }
        public IReadOnlyDictionary<int, INetworkEntity> Entities => _entities;
        public IReadOnlyLifetime Lifetime => _lifetime;
        public IViewableProperty<bool> IsAway => _isAway;

        public void AddEntity(INetworkEntity entity)
        {
            _entities.Add(entity.Id, entity);
            entity.Lifetime.Listen(() => _entities.Remove(entity.Id));
        }

        public void SetAway(bool isAway)
        {
            if (IsLocal == true || Index == NetworkUsersCollection.SessionIndex)
                throw new InvalidOperationException($"[Network] User {Index} is not a remote user: it can't be away");

            _isAway.Set(isAway);
        }

        public void DisposeRemote()
        {
            _lifetime.Terminate();
        }
    }
}