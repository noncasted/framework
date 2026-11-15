using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Game.Session;
using NUnit.Framework;
using Shared;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // A signaling channel of the host to a guest ends when the server says the connection of the guest is gone: a
    // returning guest negotiates in a new channel instead of sending its offer to the peer of the old connection.
    [NoAutoStaticsCleanup]
    public class PeerSignalingDropTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        // The guest returned to the server session before the server noticed the loss (RemoteUpdate of a known index).
        [UnityTest]
        public IEnumerator GuestReturns_OldChannelEnds() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (servers, server, match) = CreateServer(lifetime, TimeSpan.Zero);
                var guestId = Guid.NewGuid();
                var host = await PeerHostTests.StartControl(lifetime, servers, server.GetTicket(match.Id, Guid.NewGuid(), 0));
                var guest = await PeerHostTests.StartControl(lifetime, servers, server.GetTicket(match.Id, guestId, 0));

                await UniTask.WaitUntil(() => host.Peers.ContainsKey(guest.LocalIndex) == true).Timeout(Timeout);

                var attempt = lifetime.Child();
                host.Signaling.Open(attempt, guest.LocalIndex);

                await PeerHostTests.StartControl(lifetime, servers, server.GetTicket(match.Id, guestId, 0));
                await UniTask.WaitUntil(() => attempt.IsTerminated == true).Timeout(Timeout);

                // The new negotiation of the guest gets a channel of its own.
                host.Signaling.Open(lifetime.Child(), guest.LocalIndex);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        [UnityTest]
        public IEnumerator GuestAway_ChannelEnds() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (servers, server, match) = CreateServer(lifetime, TimeSpan.FromSeconds(30));
                var host = await PeerHostTests.StartControl(lifetime, servers, server.GetTicket(match.Id, Guid.NewGuid(), 0));
                var guestLifetime = lifetime.Child();
                var guest = await PeerHostTests.StartControl(guestLifetime, servers, server.GetTicket(match.Id, Guid.NewGuid(), 0));

                await UniTask.WaitUntil(() => host.Peers.ContainsKey(guest.LocalIndex) == true).Timeout(Timeout);

                var attempt = lifetime.Child();
                host.Signaling.Open(attempt, guest.LocalIndex);

                guestLifetime.Terminate();
                await UniTask.WaitUntil(() => attempt.IsTerminated == true).Timeout(Timeout);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        [UnityTest]
        public IEnumerator GuestLeaves_ChannelEnds() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (servers, server, match) = CreateServer(lifetime, TimeSpan.Zero);
                var host = await PeerHostTests.StartControl(lifetime, servers, server.GetTicket(match.Id, Guid.NewGuid(), 0));
                var guestLifetime = lifetime.Child();
                var guest = await PeerHostTests.StartControl(guestLifetime, servers, server.GetTicket(match.Id, Guid.NewGuid(), 0));

                await UniTask.WaitUntil(() => host.Peers.ContainsKey(guest.LocalIndex) == true).Timeout(Timeout);

                var attempt = lifetime.Child();
                host.Signaling.Open(attempt, guest.LocalIndex);

                guestLifetime.Terminate();
                await UniTask.WaitUntil(() => attempt.IsTerminated == true).Timeout(Timeout);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // A first offer may come before the RemoteUpdate of a new player: its channel stays.
        [UnityTest]
        public IEnumerator NewPlayer_ChannelOpenedBeforeItsUpdateStays() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (servers, server, match) = CreateServer(lifetime, TimeSpan.Zero);
                var host = await PeerHostTests.StartControl(lifetime, servers, server.GetTicket(match.Id, Guid.NewGuid(), 0));

                var attempt = lifetime.Child();
                host.Signaling.Open(attempt, host.LocalIndex + 1);

                var guest = await PeerHostTests.StartControl(lifetime, servers, server.GetTicket(match.Id, Guid.NewGuid(), 0));
                await UniTask.WaitUntil(() => host.Peers.ContainsKey(guest.LocalIndex) == true).Timeout(Timeout);

                Assert.AreEqual(host.LocalIndex + 1, guest.LocalIndex);
                Assert.IsFalse(attempt.IsTerminated);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        private static (LocalServers Servers, LocalSessionServer Server, LocalSession Match) CreateServer(
            IReadOnlyLifetime lifetime,
            TimeSpan reconnectWindow)
        {
            var servers = new LocalServers();
            var server = new LocalSessionServer(lifetime, "offline://control");
            var match = server.Create(Guid.NewGuid(), SessionType.Match, 4, _ => Array.Empty<IService>(), reconnectWindow);
            servers.Add(lifetime, "control", server);

            return (servers, server, match);
        }
    }
}
