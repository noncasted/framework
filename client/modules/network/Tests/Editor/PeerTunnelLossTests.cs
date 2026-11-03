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
    // A connection on the host ends together with its tunnel: a guest that returns (its old connection is not noticed
    // as lost yet) or tries again gets a new connection instead of frames lost in the old one.
    [NoAutoStaticsCleanup]
    public class PeerTunnelLossTests
    {
        // The guest returned to the server session before the server noticed the loss: the seat is known, and the frame
        // of the new connection must not go to the old tunnel.
        [UnityTest]
        public IEnumerator RemoteUpdateOfKnownIndex_OldTunnelIsLost() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var servers = new LocalServers();
                var server = new LocalSessionServer(lifetime, "offline://control");
                var match = server.Create(SessionType.Match, 4, _ => Array.Empty<IService>());
                servers.Add(lifetime, "control", server);

                var guestId = Guid.NewGuid();
                var host = await PeerHostTests.StartControl(lifetime, servers, server.GetTicket(match.Id, Guid.NewGuid(), 0));
                var firstGuest = await PeerHostTests.StartControl(lifetime, servers, server.GetTicket(match.Id, guestId, 0));

                var tunnel = host.Tunnels.Open(lifetime, firstGuest.LocalIndex);
                var isLost = false;
                tunnel.Closed.Advise(lifetime, _ => isLost = true);

                await PeerHostTests.StartControl(lifetime, servers, server.GetTicket(match.Id, guestId, 0));
                await UniTask.WaitUntil(() => isLost == true).Timeout(TimeSpan.FromSeconds(5));
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // A refused authorization closes the connection of the server: the socket under it is dropped too, otherwise a
        // tunnel stays open with nobody reading it and the next try of the guest is lost in it.
        [UnityTest]
        public IEnumerator AuthorizationRefused_SocketIsDropped() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var server = new LocalSessionServer(lifetime, "offline://host");
                var (clientEnd, serverEnd) = MemorySocket.Pair();
                server.AcceptSocket(lifetime, serverEnd);

                var client = new PeerHostTests.Client(lifetime, clientEnd);
                var response = await client.Writer
                                           .WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request { Ticket = "offline.unknown" })
                                           .Timeout(PeerHostTests.Timeout);

                Assert.AreEqual(SessionAuthStatus.InvalidTicket, response.Status);
                await UniTask.WaitUntil(() => serverEnd.DropReason != null).Timeout(TimeSpan.FromSeconds(5));
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        private class MemorySocket : IWebSocket
        {
            private readonly ViewableDelegate<byte[]> _received = new();
            private readonly ViewableDelegate<SocketCloseInfo> _closed = new();

            private MemorySocket _other;

            public string DropReason { get; private set; }
            public IViewableDelegate<byte[]> Received => _received;
            public IViewableDelegate<SocketCloseInfo> Closed => _closed;

            public static (MemorySocket, MemorySocket) Pair()
            {
                var first = new MemorySocket();
                var second = new MemorySocket();
                first._other = second;
                second._other = first;
                return (first, second);
            }

            public UniTask Connect() => UniTask.CompletedTask;

            public UniTask Send(ArraySegment<byte> bytes)
            {
                _other._received.Invoke(bytes.ToArray());
                return UniTask.CompletedTask;
            }

            public void Drop(string reason)
            {
                DropReason = reason;
            }
        }
    }
}
