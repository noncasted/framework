using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Session;
using NUnit.Framework;
using Shared;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // A P2P match inside one process: the host runs the session core on its local server, its own client enters it
    // through a loopback socket, a guest through a WebRTC data channel (libdatachannel, host candidates). Both are
    // users of one session: they learn each other, an entity of the guest reaches the host, the loss of the guest
    // ends its seat for the host.
    [NoAutoStaticsCleanup]
    public class PeerHostTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [UnityTest]
        public IEnumerator HostAndGuest_ShareOneSession() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var server = new LocalSessionServer(lifetime, "offline://host");
                var session = server.Create(SessionType.Match, 4, _ => Array.Empty<IService>());

                var host = new Client(lifetime, new LoopbackWebSocket("offline://host", server, lifetime));
                await host.Connect();
                await host.Authorize(server.GetTicket(session.Id, Guid.NewGuid(), 0));
                var hostUpdate = await host.WaitOneWay<SharedSessionPlayer.LocalUpdate>();

                var guestLifetime = lifetime.Child();
                var (guestSocket, hostSocket) = await ConnectPeers(lifetime, guestLifetime);
                server.AcceptSocket(lifetime, hostSocket);

                var guest = new Client(guestLifetime, guestSocket);
                await guest.Authorize(server.GetTicket(session.Id, Guid.NewGuid(), 0));
                var guestUpdate = await guest.WaitOneWay<SharedSessionPlayer.LocalUpdate>();

                Assert.AreEqual(1, hostUpdate.Index);
                Assert.AreEqual(2, guestUpdate.Index);
                Assert.AreEqual(1, (await guest.WaitOneWay<SharedSessionPlayer.RemoteUpdate>()).Index);
                Assert.AreEqual(2, (await host.WaitOneWay<SharedSessionPlayer.RemoteUpdate>()).Index);

                var entityId = SharedEntityIds.Create(guestUpdate.Index, 1);

                await guest.Writer.WriteRequest<SharedSessionEntity.CreateResponse>(new SharedSessionEntity.CreateRequest
                {
                    // No payload: the union of the payloads in a game project is the game's own.
                    Id = entityId,
                    Payload = null,
                    Properties = Array.Empty<SharedSessionObject.PropertyUpdate>()
                }).Timeout(Timeout);

                Assert.AreEqual(entityId, (await host.WaitOneWay<SharedSessionEntity.CreatedOverview>()).EntityId);

                guestLifetime.Terminate();

                Assert.AreEqual(2, (await host.WaitOneWay<SharedSessionPlayer.RemoteDisconnect>()).Index);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // The whole P2P match in one process: the server session of the match (the control plane: the core relays the
        // signals and the tunnel, as the backend) on one local server, the session of the game on the local server of
        // the host. One guest connects to the host directly, the other one through the tunnel of the server; their
        // seats keep the indices of the server session.
        [UnityTest]
        public IEnumerator DirectAndTunnelGuests_PlayOnHost() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var localServers = new LocalServers();
                var control = new LocalSessionServer(lifetime, "offline://control");
                var match = control.Create(SessionType.Match, 3, _ => Array.Empty<IService>());
                localServers.Add(lifetime, "control", control);

                var hostControl = await StartControl(lifetime, localServers, control.GetTicket(match.Id, Guid.NewGuid(), 0));
                var directControl = await StartControl(lifetime, localServers, control.GetTicket(match.Id, Guid.NewGuid(), 0));
                var tunnelLifetime = lifetime.Child();
                var tunnelControl = await StartControl(tunnelLifetime, localServers, control.GetTicket(match.Id, Guid.NewGuid(), 0));

                Assert.AreEqual(new[] { 1, 2, 3 }, new[] { hostControl.LocalIndex, directControl.LocalIndex, tunnelControl.LocalIndex });

                // The host: the session of the game under the id of the server session.
                var game = new LocalSessionServer(lifetime, "offline://game");
                game.Create(match.Id, SessionType.Match, 3, _ => Array.Empty<IService>());
                var hostMatch = new PeerMatch();
                hostMatch.SetHost(hostControl.LocalIndex);
                new PeerHost(hostControl, game, hostMatch, Options()).Run(lifetime);

                var host = new Client(lifetime, new LoopbackWebSocket("offline://game", game, lifetime));
                await host.Connect();
                await host.Authorize(Ticket(match.Id, hostControl));

                var directMatch = new PeerMatch();
                var directSocket = new PeerHostSocket(directControl, hostControl.LocalIndex, directMatch, Options(), true, lifetime);
                var direct = new Client(lifetime, directSocket);
                await directSocket.Connect();
                await direct.Authorize(Ticket(match.Id, directControl));

                var tunnelMatch = new PeerMatch();
                var tunnelSocket = new PeerHostSocket(tunnelControl, hostControl.LocalIndex, tunnelMatch, Options(), false, tunnelLifetime);
                var tunnel = new Client(tunnelLifetime, tunnelSocket);
                await tunnelSocket.Connect();
                await tunnel.Authorize(Ticket(match.Id, tunnelControl));

                Assert.AreEqual(PeerRoute.Direct, directSocket.Route);
                Assert.AreEqual(PeerRoute.Tunnel, tunnelSocket.Route);
                Assert.AreEqual(PeerRoute.Direct, directMatch.Routes[2]);
                Assert.AreEqual(PeerRoute.Tunnel, tunnelMatch.Routes[3]);
                Assert.AreEqual(PeerRoute.Direct, hostMatch.Routes[2]);
                Assert.AreEqual(PeerRoute.Tunnel, hostMatch.Routes[3]);
                Assert.AreEqual(2, (await direct.WaitOneWay<SharedSessionPlayer.LocalUpdate>()).Index);
                Assert.AreEqual(3, (await tunnel.WaitOneWay<SharedSessionPlayer.LocalUpdate>()).Index);

                // An entity of the guest behind the tunnel reaches the host and the direct guest.
                var entityId = SharedEntityIds.Create(3, 1);

                await tunnel.Writer.WriteRequest<SharedSessionEntity.CreateResponse>(new SharedSessionEntity.CreateRequest
                {
                    Id = entityId,
                    Payload = null,
                    Properties = Array.Empty<SharedSessionObject.PropertyUpdate>()
                }).Timeout(Timeout);

                Assert.AreEqual(entityId, (await host.WaitOneWay<SharedSessionEntity.CreatedOverview>()).EntityId);
                Assert.AreEqual(entityId, (await direct.WaitOneWay<SharedSessionEntity.CreatedOverview>()).EntityId);

                // The guest behind the tunnel leaves the server session: its tunnel and its seat on the host end.
                tunnelLifetime.Terminate();

                Assert.AreEqual(3, (await host.WaitOneWay<SharedSessionPlayer.RemoteDisconnect>()).Index);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        private static WebRtcPeerOptions Options()
        {
            return new WebRtcPeerOptions(Array.Empty<string>(), Timeout);
        }

        private static string Ticket(Guid matchId, ISessionControl control)
        {
            return LocalSessionServer.CreateTicket(matchId, control.LocalId, control.LocalIndex);
        }

        private static async UniTask<SessionControl> StartControl(IReadOnlyLifetime lifetime, ILocalServers servers, string ticket)
        {
            var control = new SessionControl(new PlatformOptions(), new SilentNotice(), servers, new PeerSockets(),
                new NetworkProtocolHash("0123456789abcdef"));

            await control.Start(lifetime, "offline://control", ticket).Timeout(Timeout);
            return control;
        }

        private class SilentNotice : IClientOutdatedNotice
        {
            public bool IsShown => false;

            public void Show()
            {
            }
        }

        // The guest offers, the host answers: a pair of sockets negotiated through a signaling pair in memory.
        private static async UniTask<(IWebSocket Guest, IWebSocket Host)> ConnectPeers(
            IReadOnlyLifetime hostLifetime,
            IReadOnlyLifetime guestLifetime)
        {
            var options = new WebRtcPeerOptions(Array.Empty<string>(), Timeout);
            var guestEnd = new SignalingEnd();
            var hostEnd = new SignalingEnd();
            guestEnd.Other = hostEnd;
            hostEnd.Other = guestEnd;

            var guest = new WebRtcPeerSocket(WebRtcPeerRole.Offer, guestEnd, options, guestLifetime);
            var host = new WebRtcPeerSocket(WebRtcPeerRole.Answer, hostEnd, options, hostLifetime);

            var guestConnect = guest.Connect();
            var hostConnect = host.Connect();
            await UniTask.WhenAll(guestConnect, hostConnect);

            return (guest, host);
        }

        private class SignalingEnd : IWebRtcSignaling
        {
            private readonly ViewableDelegate<WebRtcSignal> _received = new();

            public SignalingEnd Other;

            public IViewableDelegate<WebRtcSignal> Received => _received;

            public void Send(WebRtcSignal signal)
            {
                Other._received.Invoke(signal);
            }
        }

        // The client stack (reader, writer) over a socket, as NetworkConnection runs it.
        private class Client
        {
            public Client(IReadOnlyLifetime lifetime, IWebSocket socket)
            {
                var traffic = new NetworkTraffic();

                _socket = socket;
                Writer = new ConnectionWriter(traffic);
                _reader = new ConnectionReader(traffic);

                _reader.Run(lifetime, socket);
                Writer.Run(lifetime, socket);
                _reader.Response.Advise(lifetime, response => Writer.OnRequestHandled(response.Context, response.RequestId));
                _reader.OneWay.Advise(lifetime, message => _received.Add(message.Context));
            }

            private readonly IWebSocket _socket;
            private readonly ConnectionReader _reader;
            private readonly List<INetworkContext> _received = new();

            public ConnectionWriter Writer { get; }

            public UniTask Connect()
            {
                return _socket.Connect();
            }

            public async UniTask Authorize(string ticket)
            {
                var response = await Writer
                                     .WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request { Ticket = ticket })
                                     .Timeout(Timeout);

                Assert.AreEqual(SessionAuthStatus.Ok, response.Status);
            }

            public async UniTask<T> WaitOneWay<T>() where T : class, INetworkContext
            {
                await UniTask.WaitUntil(() => _received.Exists(context => context is T)).Timeout(Timeout);
                return (T)_received.Find(context => context is T);
            }
        }
    }
}
