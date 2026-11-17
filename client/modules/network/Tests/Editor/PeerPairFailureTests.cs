using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Shared;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // The direct connection of a pair can't even be created (the link throws in its constructor: an unsupported
    // platform or channel layout): the attempt ends with its signaling channel and the pair goes through the tunnel.
    [NoAutoStaticsCleanup]
    public class PeerPairFailureTests
    {
        private const string Failure = "test: the link can't listen to the signaling";

        [UnityTest]
        public IEnumerator LinkConstructorThrows_ChannelEndsAndTunnelFollows() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var control = new FailingControl();
                var mesh = new PeerMesh(control,
                    new WebRtcPeerOptions(Array.Empty<string>(), TimeSpan.FromSeconds(10)),
                    new PeerMeshOptions(true, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(2)));

                mesh.Run(lifetime);

                LogAssert.Expect(LogType.Exception, new Regex(Failure));
                control.AddPeer(2);

                Assert.AreEqual(1, control.Channels.Count, "the pair opened no signaling channel");
                Assert.IsTrue(control.Channels[0].IsTerminated, "the signaling channel of the failed attempt stays open");
                Assert.AreEqual(1, control.TunnelsOpened, "the pair did not fall back to the tunnel");
                Assert.AreEqual(PeerRoute.Tunnel, mesh.Links[2].Route);

                await UniTask.Yield();
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        private sealed class FailingControl : ISessionControl, IPeerSignaling, IPeerTunnels
        {
            private readonly ViewableDictionary<int, Guid> _peers = new();

            public readonly List<IReadOnlyLifetime> Channels = new();
            public int TunnelsOpened;

            public int LocalIndex => 1;
            public Guid LocalId => Guid.Empty;
            public IViewableDictionary<int, Guid> Peers => _peers;
            public IPeerSignaling Signaling => this;
            public IPeerTunnels Tunnels => this;
            public IViewableDelegate<SocketCloseInfo> Closed { get; } = new ViewableDelegate<SocketCloseInfo>();

            IViewableDelegate<int> IPeerSignaling.Incoming { get; } = new ViewableDelegate<int>();
            IViewableDelegate<int> IPeerTunnels.Incoming { get; } = new ViewableDelegate<int>();

            public void AddPeer(int index)
            {
                _peers.Add(index, Guid.NewGuid());
            }

            public IWebRtcSignaling Open(ILifetime lifetime, int remoteIndex)
            {
                Channels.Add(lifetime);
                return new ThrowingSignaling();
            }

            IWebSocket IPeerTunnels.Open(IReadOnlyLifetime lifetime, int remoteIndex)
            {
                TunnelsOpened++;
                return new SilentSocket();
            }

            public UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, string ticket) => throw new NotSupportedException();

            public UniTask<SharedSessionService.GetResponse> GetService(
                IReadOnlyLifetime lifetime,
                string key,
                IReadOnlyList<int> propertyIds,
                Action<SharedSessionObject.PropertyUpdate> onUpdate) => throw new NotSupportedException();

            public void WriteProperty(int objectId, int propertyId, byte[] value) => throw new NotSupportedException();
            public UniTask Leave() => throw new NotSupportedException();
        }

        private sealed class ThrowingSignaling : IWebRtcSignaling, IViewableDelegate<WebRtcSignal>
        {
            public IViewableDelegate<WebRtcSignal> Received => this;

            public void Advise(IReadOnlyLifetime lifetime, Action<WebRtcSignal> handler)
            {
                throw new InvalidOperationException(Failure);
            }

            public void Dispose()
            {
            }

            public void Send(WebRtcSignal signal)
            {
            }
        }

        private sealed class SilentSocket : IWebSocket
        {
            public IViewableDelegate<byte[]> Received { get; } = new ViewableDelegate<byte[]>();
            public IViewableDelegate<SocketCloseInfo> Closed { get; } = new ViewableDelegate<SocketCloseInfo>();

            public UniTask Connect() => UniTask.CompletedTask;
            public UniTask Send(ArraySegment<byte> bytes) => UniTask.CompletedTask;

            public void Drop(string reason)
            {
            }
        }
    }
}
