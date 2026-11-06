using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Game.Session;
using NUnit.Framework;
using Shared;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // The full mesh of a session in one process: the server session of the match on a local server, its clients through
    // loopback control connections, a PeerMesh in every client. The pairs negotiate direct connections (libdatachannel,
    // host candidates) or go through the tunnel of the server; a lost connection is negotiated again.
    [NoAutoStaticsCleanup]
    public class PeerMeshTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

        private static readonly WebRtcChannelSpec[] Specs =
        {
            new("records", WebRtcChannelMode.ReliableOrdered),
            new("media", WebRtcChannelMode.ReliableOrdered),
            new("presence", WebRtcChannelMode.Unreliable)
        };

        [UnityTest]
        public IEnumerator MeshOfThreeFiveAndEight_AllDirect() => UniTask.ToCoroutine(async () =>
        {
            foreach (var count in new[] { 3, 5, 8 })
            {
                var lifetime = new Lifetime();

                try
                {
                    var (servers, server, session) = CreateServer(lifetime);
                    var peers = new List<MeshPeer>();

                    for (var i = 0; i < count; i++)
                        peers.Add(await Join(lifetime, servers, server, session, Guid.NewGuid()));

                    await WaitFullMesh(peers, Timeout);

                    foreach (var peer in peers)
                    {
                        Assert.AreEqual(count - 1, peer.Mesh.DirectLinks.Value, $"{peer.Index}: direct links");
                        Assert.AreEqual(0, peer.Mesh.TunnelLinks.Value, $"{peer.Index}: tunnel links");

                        foreach (var link in peer.Mesh.Links.Values)
                            Assert.AreEqual(PeerRoute.Direct, link.Route, $"{peer.Index} -> {link.RemoteIndex}");
                    }

                    await AssertChannelsWork(peers, lifetime);
                }
                finally
                {
                    lifetime.Terminate();
                }
            }
        });

        [UnityTest]
        public IEnumerator LateAndSimultaneousEntry_FullMesh() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (servers, server, session) = CreateServer(lifetime);
                var first = await Join(lifetime, servers, server, session, Guid.NewGuid());

                // Two players enter at the same time: their authorizations and first updates interleave.
                var second = Join(lifetime, servers, server, session, Guid.NewGuid());
                var third = Join(lifetime, servers, server, session, Guid.NewGuid());
                var (secondPeer, thirdPeer) = await UniTask.WhenAll(second, third);

                var fourth = await Join(lifetime, servers, server, session, Guid.NewGuid());
                var peers = new List<MeshPeer> { first, secondPeer, thirdPeer, fourth };

                await WaitFullMesh(peers, Timeout);

                foreach (var peer in peers)
                    foreach (var link in peer.Mesh.Links.Values)
                        Assert.AreEqual(PeerRoute.Direct, link.Route, $"{peer.Index} -> {link.RemoteIndex}");

                await AssertChannelsWork(peers, lifetime);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // The offer of a player may reach the answerer before its RemoteUpdate: the pair is created by the signal and
        // the link waits for the id of the player. A second channel of the signaling must not be opened.
        [UnityTest]
        public IEnumerator OfferBeforeRemoteUpdate_Connects() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (servers, server, session) = CreateServer(lifetime);
                var offerer = await Join(lifetime, servers, server, session, Guid.NewGuid());

                var (answerer, delayed) = await JoinDelayed(lifetime, servers, server, session, Guid.NewGuid());

                var isOfferArrived = false;
                answerer.Control.Signaling.Incoming.Advise(lifetime, _ => isOfferArrived = true);

                await UniTask.WaitUntil(() => isOfferArrived == true).Timeout(Timeout);

                Assert.AreEqual(0, delayed.Count, "the update of the player must still be held back");
                Assert.AreEqual(0, answerer.Mesh.Links.Count, "the link waits for the id of the player");

                delayed.Release();

                await UniTask.WaitUntil(() => answerer.Mesh.Links.Count == 1 && offerer.Mesh.Links.Count == 1).Timeout(Timeout);
                Assert.AreEqual(PeerRoute.Direct, answerer.Mesh.Links[offerer.Index].Route);

                await AssertChannelsWork(new[] { offerer, answerer }, lifetime);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // The offerer publishes its link and sends at once, while the link of the answerer still waits for the id of the
        // player: the frames of that gap reach a subscriber that attaches when the link of the answerer appears, in
        // their order. Both routes: a direct connection and the tunnel of the server.
        [UnityTest]
        public IEnumerator FramesBeforePublication_Delivered() => UniTask.ToCoroutine(async () =>
        {
            foreach (var isDirect in new[] { true, false })
            {
                var lifetime = new Lifetime();

                try
                {
                    var (servers, server, session) = CreateServer(lifetime);
                    var offerer = await Join(lifetime, servers, server, session, Guid.NewGuid(), isDirectAllowed: isDirect);
                    var (answerer, delayed) = await JoinDelayed(lifetime, servers, server, session, Guid.NewGuid());

                    var received = new List<(int Channel, byte Value)>();

                    answerer.Mesh.Links.Advise(lifetime, (_, _, link) =>
                    {
                        for (var channel = 0; channel < 2; channel++)
                        {
                            var current = channel;
                            link.Received(current).Advise(lifetime, bytes => received.Add((current, bytes[0])));
                        }
                    });

                    await UniTask.WaitUntil(() => offerer.Mesh.Links.Count == 1).Timeout(Timeout);

                    var route = isDirect ? PeerRoute.Direct : PeerRoute.Tunnel;
                    Assert.AreEqual(route, offerer.Mesh.Links[answerer.Index].Route, "the route of the offerer");

                    var link = offerer.Mesh.Links[answerer.Index];

                    for (byte i = 0; i < 10; i++)
                        link.Send(i % 2, new ArraySegment<byte>(new[] { i }));

                    // The frames arrive at the answerer and wait there: its link has no id yet.
                    await UniTask.Delay(TimeSpan.FromSeconds(1));
                    Assert.AreEqual(0, answerer.Mesh.Links.Count, "the link waits for the id of the player");

                    delayed.Release();

                    await UniTask.WaitUntil(() => received.Count == 10).Timeout(Timeout);
                    Assert.AreEqual(route, answerer.Mesh.Links[offerer.Index].Route, "the route of the answerer");

                    for (var channel = 0; channel < 2; channel++)
                    {
                        var values = received.Where(frame => frame.Channel == channel).Select(frame => (int)frame.Value);
                        var expected = Enumerable.Range(0, 10).Where(value => value % 2 == channel);
                        CollectionAssert.AreEqual(expected, values, $"the frames of channel {channel}");
                    }
                }
                finally
                {
                    lifetime.Terminate();
                }
            }
        });

        [UnityTest]
        public IEnumerator PeerLeavesAndReturns_LinksRestored() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (servers, server, session) = CreateServer(lifetime);
                var first = await Join(lifetime, servers, server, session, Guid.NewGuid());
                var second = await Join(lifetime, servers, server, session, Guid.NewGuid());
                var leavingId = Guid.NewGuid();
                var leaving = await Join(lifetime, servers, server, session, leavingId);

                await WaitFullMesh(new[] { first, second, leaving }, Timeout);
                var leavingIndex = leaving.Index;

                await leaving.Control.Leave();
                leaving.Lifetime.Terminate();

                await UniTask.WaitUntil(() => first.Mesh.Links.Count == 1 && second.Mesh.Links.Count == 1).Timeout(Timeout);
                Assert.IsFalse(first.Mesh.Links.ContainsKey(leavingIndex), "the link of the player that left is gone");

                var returned = await Join(lifetime, servers, server, session, leavingId);
                await WaitFullMesh(new[] { first, second, returned }, Timeout);

                await AssertChannelsWork(new[] { first, second, returned }, lifetime);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // The direct connection of one side is broken: both pairs lose their links and negotiate again, and the link
        // comes back without the session.
        [UnityTest]
        public IEnumerator DirectLoss_Reconnects() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (servers, server, session) = CreateServer(lifetime);
                var first = await Join(lifetime, servers, server, session, Guid.NewGuid());
                var second = await Join(lifetime, servers, server, session, Guid.NewGuid());

                await WaitFullMesh(new[] { first, second }, Timeout);
                DropDirect(first.Mesh, second.Index);

                await UniTask.WaitUntil(() => first.Mesh.Links.Count == 0 && second.Mesh.Links.Count == 0).Timeout(Timeout);
                await WaitFullMesh(new[] { first, second }, Timeout);

                Assert.AreEqual(PeerRoute.Direct, first.Mesh.Links[second.Index].Route);
                await AssertChannelsWork(new[] { first, second }, lifetime);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // The offering side has no direct connection: it opens the tunnel at once, and the answerer cancels its
        // negotiation when the first frame comes.
        [UnityTest]
        public IEnumerator PairThroughTunnel_AllChannelsWork() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (servers, server, session) = CreateServer(lifetime);
                var first = await Join(lifetime, servers, server, session, Guid.NewGuid(), isDirectAllowed: false);
                var second = await Join(lifetime, servers, server, session, Guid.NewGuid());

                await WaitFullMesh(new[] { first, second }, Timeout);

                Assert.AreEqual(PeerRoute.Tunnel, first.Mesh.Links[second.Index].Route);
                Assert.AreEqual(PeerRoute.Tunnel, second.Mesh.Links[first.Index].Route);
                Assert.AreEqual(1, first.Mesh.TunnelLinks.Value);
                Assert.AreEqual(0, first.Mesh.DirectLinks.Value);

                await AssertChannelsWork(new[] { first, second }, lifetime);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // One player of four has no direct connections: its links go through the server, the links of the others stay
        // direct. The offering sides wait for their timeout and fall back to the tunnel.
        [UnityTest]
        public IEnumerator MixedMesh_OnePeerThroughTunnel() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var connectTimeout = TimeSpan.FromSeconds(2);

            try
            {
                var (servers, server, session) = CreateServer(lifetime);
                var peers = new List<MeshPeer>();

                for (var i = 0; i < 3; i++)
                    peers.Add(await Join(lifetime, servers, server, session, Guid.NewGuid(), connectTimeout: connectTimeout));

                var tunnel = await Join(lifetime, servers, server, session, Guid.NewGuid(), isDirectAllowed: false,
                    connectTimeout: connectTimeout);
                peers.Add(tunnel);

                await WaitFullMesh(peers, Timeout);

                foreach (var peer in peers)
                {
                    foreach (var link in peer.Mesh.Links.Values)
                    {
                        var expected = peer == tunnel || link.RemoteIndex == tunnel.Index
                            ? PeerRoute.Tunnel
                            : PeerRoute.Direct;

                        Assert.AreEqual(expected, link.Route, $"{peer.Index} -> {link.RemoteIndex}");
                    }
                }

                Assert.AreEqual(3, tunnel.Mesh.TunnelLinks.Value, "the player without direct connections");
                Assert.AreEqual(0, tunnel.Mesh.DirectLinks.Value);
                Assert.AreEqual(2, peers[0].Mesh.DirectLinks.Value);
                Assert.AreEqual(1, peers[0].Mesh.TunnelLinks.Value);

                await AssertChannelsWork(peers, lifetime);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // The player without direct connections answers a direct offer (a smaller index): it opens the tunnel itself,
        // and the offering side moves there at once, not after the timeout of its direct connection.
        [UnityTest]
        public IEnumerator TunnelPeerAnswersOffer_NoDirectTimeout() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var connectTimeout = TimeSpan.FromSeconds(60);

            try
            {
                var (servers, server, session) = CreateServer(lifetime);
                var first = await Join(lifetime, servers, server, session, Guid.NewGuid(), connectTimeout: connectTimeout);
                var tunnel = await Join(lifetime, servers, server, session, Guid.NewGuid(), isDirectAllowed: false,
                    connectTimeout: connectTimeout);
                var last = await Join(lifetime, servers, server, session, Guid.NewGuid(), connectTimeout: connectTimeout);
                var peers = new List<MeshPeer> { first, tunnel, last };

                Assert.Less(first.Index, tunnel.Index, "the tunnel peer answers the first one");

                await WaitFullMesh(peers, TimeSpan.FromSeconds(10));

                Assert.AreEqual(2, tunnel.Mesh.TunnelLinks.Value, "the player without direct connections");
                Assert.AreEqual(PeerRoute.Tunnel, first.Mesh.Links[tunnel.Index].Route);
                Assert.AreEqual(PeerRoute.Direct, first.Mesh.Links[last.Index].Route);

                await AssertChannelsWork(peers, lifetime);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        private sealed class MeshPeer
        {
            public ILifetime Lifetime;
            public SessionControl Control;
            public PeerMesh Mesh;

            public int Index => Control.LocalIndex;
        }

        private static (LocalServers Servers, LocalSessionServer Server, LocalSession Session) CreateServer(IReadOnlyLifetime lifetime)
        {
            var servers = new LocalServers();
            var server = new LocalSessionServer(lifetime, "offline://control");
            var session = server.Create(SessionType.Match, 16, _ => Array.Empty<IService>());
            servers.Add(lifetime, "control", server);

            return (servers, server, session);
        }

        private static async UniTask<MeshPeer> Join(
            IReadOnlyLifetime lifetime,
            ILocalServers servers,
            LocalSessionServer server,
            LocalSession session,
            Guid userId,
            bool isDirectAllowed = true,
            TimeSpan? connectTimeout = null)
        {
            var controlLifetime = lifetime.Child();
            var control = await PeerHostTests.StartControl(controlLifetime, servers, server.GetTicket(session.Id, userId, 0));
            var mesh = new PeerMesh(
                control,
                new WebRtcPeerOptions(Array.Empty<string>(), connectTimeout ?? TimeSpan.FromSeconds(10), Specs),
                new PeerMeshOptions(isDirectAllowed, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(2)));

            mesh.Run(controlLifetime);

            return new MeshPeer { Lifetime = controlLifetime, Control = control, Mesh = mesh };
        }

        // The same join with the updates of the control held back from the mesh: the subscription comes before the
        // authorization, so even the first update of the player is held.
        private static async UniTask<(MeshPeer Peer, DelayedPeers Peers)> JoinDelayed(
            IReadOnlyLifetime lifetime,
            ILocalServers servers,
            LocalSessionServer server,
            LocalSession session,
            Guid userId)
        {
            var controlLifetime = lifetime.Child();
            var control = new SessionControl(
                new PlatformOptions(),
                new PeerHostTests.SilentNotice(),
                servers,
                new PeerSockets(),
                new NetworkProtocolHash("0123456789abcdef"));

            var peers = new DelayedPeers(lifetime, control.Peers);

            await control.Start(controlLifetime, "offline://control", server.GetTicket(session.Id, userId, 0)).Timeout(Timeout);

            var mesh = new PeerMesh(new DelayedControl(control, peers),
                new WebRtcPeerOptions(Array.Empty<string>(), TimeSpan.FromSeconds(10), Specs),
                new PeerMeshOptions(true, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(2)));

            mesh.Run(controlLifetime);

            return (new MeshPeer { Lifetime = controlLifetime, Control = control, Mesh = mesh }, peers);
        }

        private static async UniTask WaitFullMesh(IReadOnlyList<MeshPeer> peers, TimeSpan timeout)
        {
            await UniTask.WaitUntil(() => peers.All(peer => peer.Mesh.Links.Count == peers.Count - 1)).Timeout(timeout);
        }

        // A message on every channel of every ordered pair of the mesh reaches its receiver with the same channel.
        private static async UniTask AssertChannelsWork(IReadOnlyList<MeshPeer> peers, IReadOnlyLifetime lifetime)
        {
            var expected = peers.Count * (peers.Count - 1) * Specs.Length;
            var received = new List<(int From, int Channel, byte[] Bytes)>();

            foreach (var to in peers)
            {
                foreach (var link in to.Mesh.Links.Values)
                {
                    var from = peers.Single(peer => peer.Index == link.RemoteIndex);

                    for (var channel = 0; channel < Specs.Length; channel++)
                    {
                        var current = channel;
                        link.Received(current).Advise(lifetime, bytes => received.Add((from.Index, current, bytes)));
                    }
                }
            }

            foreach (var from in peers)
            {
                foreach (var link in from.Mesh.Links.Values)
                {
                    for (var channel = 0; channel < Specs.Length; channel++)
                        link.Send(channel, new ArraySegment<byte>(new[] { (byte)from.Index, (byte)channel }));
                }
            }

            await UniTask.WaitUntil(() => received.Count == expected).Timeout(Timeout);

            foreach (var (from, channel, bytes) in received)
            {
                Assert.AreEqual(2, bytes.Length, "the size of the message");
                Assert.AreEqual((byte)from, bytes[0], "the sender of the message");
                Assert.AreEqual((byte)channel, bytes[1], "the channel of the message");
            }
        }

        // Breaks the direct connection of the pair on one side: as a failed network, not as a leave of the session.
        private static void DropDirect(PeerMesh mesh, int remoteIndex)
        {
            var pairs = (IDictionary)typeof(PeerMesh)
                                         .GetField("_pairs", BindingFlags.Instance | BindingFlags.NonPublic)
                                         .GetValue(mesh);
            var pair = pairs[remoteIndex];
            var direct = (WebRtcPeerLink)pair.GetType()
                                             .GetField("_direct", BindingFlags.Instance | BindingFlags.NonPublic)
                                             .GetValue(pair);

            direct.Drop("test: the direct connection is broken");
        }

        // The peers of a real control with the update of one player held back: the offer of that player comes first.
        private sealed class DelayedControl : ISessionControl
        {
            public DelayedControl(ISessionControl source, IViewableDictionary<int, Guid> peers)
            {
                _source = source;
                Peers = peers;
            }

            private readonly ISessionControl _source;

            public int LocalIndex => _source.LocalIndex;
            public Guid LocalId => _source.LocalId;
            public IViewableDictionary<int, Guid> Peers { get; }
            public IPeerSignaling Signaling => _source.Signaling;
            public IPeerTunnels Tunnels => _source.Tunnels;
            public IViewableDelegate<SocketCloseInfo> Closed => _source.Closed;

            public UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, string ticket)
            {
                return _source.Start(lifetime, serverUrl, ticket);
            }

            public UniTask<SharedSessionService.GetResponse> GetService(
                IReadOnlyLifetime lifetime,
                string key,
                IReadOnlyList<int> propertyIds,
                Action<SharedSessionObject.PropertyUpdate> onUpdate)
            {
                return _source.GetService(lifetime, key, propertyIds, onUpdate);
            }

            public void WriteProperty(int objectId, int propertyId, byte[] value)
            {
                _source.WriteProperty(objectId, propertyId, value);
            }

            public UniTask Leave()
            {
                return _source.Leave();
            }
        }

        // The dictionary of the control with the entries held back until Release, and the ends of the entries of the
        // control reported to its own entries.
        private sealed class DelayedPeers : IViewableDictionary<int, Guid>
        {
            public DelayedPeers(IReadOnlyLifetime lifetime, IViewableDictionary<int, Guid> source)
            {
                source.Advise(lifetime, OnSourceEntry);
            }

            private readonly ViewableDictionary<int, Guid> _visible = new();
            private readonly Dictionary<int, Guid> _held = new();
            private bool _isHolding = true;

            public int Count => _visible.Count;
            public Guid this[int key] => _visible[key];
            public IEnumerable<int> Keys => _visible.Keys;
            public IEnumerable<Guid> Values => _visible.Values;

            public void Release()
            {
                _isHolding = false;

                foreach (var entry in _held.ToArray())
                {
                    if (_held.ContainsKey(entry.Key) == false)
                        continue;

                    _held.Remove(entry.Key);
                    _visible.Set(entry.Key, entry.Value);
                }
            }

            public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, int, Guid> handler)
            {
                _visible.Advise(lifetime, handler);
            }

            public IReadOnlyLifetime GetLifetime(int key)
            {
                return _visible.GetLifetime(key);
            }

            public bool ContainsKey(int key)
            {
                return _visible.ContainsKey(key);
            }

            public bool TryGetValue(int key, out Guid value)
            {
                return _visible.TryGetValue(key, out value);
            }

            public IEnumerator<KeyValuePair<int, Guid>> GetEnumerator()
            {
                return _visible.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Dispose()
            {
                _visible.Dispose();
            }

            private void OnSourceEntry(IReadOnlyLifetime entryLifetime, int index, Guid id)
            {
                entryLifetime.Listen(() => {
                    _held.Remove(index);

                    if (_visible.ContainsKey(index) == true)
                        _visible.Remove(index);
                });

                if (_isHolding == true)
                {
                    _held[index] = id;
                    return;
                }

                _visible.Set(index, id);
            }
        }
    }
}
