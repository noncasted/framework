using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Session;
using NUnit.Framework;
using Shared;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // A long frame of the host: the open of the channel of a guest and its first frame reach the socket in one frame of
    // the host. The guest here is a raw peer that sends its authorization inside its open callback, so nothing on the
    // main thread can separate the two.
    [NoAutoStaticsCleanup]
    public class PeerHostFirstFrameTests
    {
        private const int GuestIndex = 2;

        [UnityTest]
        public IEnumerator OpenAndFirstFrameInOneFrame_GuestIsAuthorized() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var server = new LocalSessionServer(lifetime, "offline://game");
                var session = server.Create(SessionType.Match, 4, _ => Array.Empty<IService>());
                var options = new WebRtcPeerOptions(Array.Empty<string>(), TimeSpan.FromSeconds(10));
                var control = new FakeControl();

                new PeerHost(control, server, new PeerMatch(), options).Run(lifetime);

                var frame = await EncodeAuth(lifetime, server.GetTicket(session.Id, Guid.NewGuid(), GuestIndex));
                var guest = new RawGuest(frame);
                guest.Peer = WebRtcPeers.Create(options, guest);
                lifetime.Listen(guest.Peer.Dispose);

                guest.Peer.CreateChannel("session");

                var deadline = Time.realtimeSinceStartup + 8f;

                while (session.Core.Users.Count == 0 && Time.realtimeSinceStartup < deadline)
                {
                    while (guest.Signals.TryDequeue(out var signal) == true)
                        control.DeliverToHost(GuestIndex, signal);

                    while (control.ToGuest.TryDequeue(out var signal) == true)
                        guest.Deliver(signal);

                    // The long frame.
                    Thread.Sleep(40);
                    await UniTask.Yield();
                }

                Assert.IsTrue(guest.IsOpened, "the channel did not open: the test proves nothing");
                Assert.AreEqual(1, session.Core.Users.Count, "the first frame of the guest was lost");
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        private static async UniTask<byte[]> EncodeAuth(IReadOnlyLifetime lifetime, string ticket)
        {
            var capture = new CaptureSocket();
            var traffic = new NetworkTraffic();
            var writer = new ConnectionWriter(traffic);
            var writerLifetime = lifetime.Child();

            writer.Run(writerLifetime, capture);
            writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request { Ticket = ticket }).NoAwait();

            await UniTask.WaitUntil(() => capture.Frame != null).Timeout(TimeSpan.FromSeconds(5));
            return capture.Frame;
        }

        private class RawGuest : IWebRtcPeerEvents
        {
            public RawGuest(byte[] firstFrame)
            {
                _firstFrame = firstFrame;
            }

            private readonly byte[] _firstFrame;

            public readonly ConcurrentQueue<WebRtcSignal> Signals = new();
            public IWebRtcPeer Peer;
            public volatile bool IsOpened;

            public void Deliver(WebRtcSignal signal)
            {
                if (signal.Kind == WebRtcSignalKind.Description)
                    Peer.SetRemoteDescription(signal.Type, signal.Value);
                else
                    Peer.AddRemoteCandidate(signal.Value, signal.Mid);
            }

            public void OnLocalDescription(string type, string sdp) => Signals.Enqueue(WebRtcSignal.Description(type, sdp));
            public void OnLocalCandidate(string candidate, string mid) => Signals.Enqueue(WebRtcSignal.Candidate(candidate, mid));

            public void OnOpen()
            {
                IsOpened = true;
                WebRtcFraming.Split(_firstFrame, WebRtcFraming.PayloadSize(Peer.MaxMessageSize), Peer.Send);
            }

            public void OnMessage(byte[] bytes)
            {
            }

            public void OnLost(string reason)
            {
            }
        }

        private class FakeControl : ISessionControl, IPeerSignaling
        {
            private readonly ViewableDelegate<int> _incoming = new();
            private readonly Dictionary<int, Channel> _channels = new();

            public readonly ConcurrentQueue<WebRtcSignal> ToGuest = new();

            public int LocalIndex => 1;
            public Guid LocalId => Guid.Empty;
            public IViewableDictionary<int, Guid> Peers { get; } = new ViewableDictionary<int, Guid>();
            public IPeerSignaling Signaling => this;
            public IPeerTunnels Tunnels { get; } = new NoTunnels();
            public IViewableDelegate<SocketCloseInfo> Closed { get; } = new ViewableDelegate<SocketCloseInfo>();
            public IViewableDelegate<int> Incoming => _incoming;

            public IWebRtcSignaling Open(IReadOnlyLifetime lifetime, int remoteIndex)
            {
                var channel = new Channel(this);
                _channels.Add(remoteIndex, channel);
                lifetime.Listen(() => _channels.Remove(remoteIndex));
                return channel;
            }

            public void DeliverToHost(int index, WebRtcSignal signal)
            {
                if (_channels.ContainsKey(index) == false)
                    _incoming.Invoke(index);

                _channels[index].Receive(signal);
            }

            public UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, string ticket) => throw new NotSupportedException();

            public UniTask<SharedSessionService.GetResponse> GetService(
                IReadOnlyLifetime lifetime,
                string key,
                IReadOnlyList<int> propertyIds,
                Action<SharedSessionObject.PropertyUpdate> onUpdate) => throw new NotSupportedException();

            public void WriteProperty(int objectId, int propertyId, byte[] value) => throw new NotSupportedException();
            public UniTask Leave() => throw new NotSupportedException();

            private class Channel : IWebRtcSignaling
            {
                public Channel(FakeControl control)
                {
                    _control = control;
                }

                private readonly FakeControl _control;
                private readonly ViewableDelegate<WebRtcSignal> _received = new();

                public IViewableDelegate<WebRtcSignal> Received => _received;

                public void Send(WebRtcSignal signal)
                {
                    _control.ToGuest.Enqueue(signal);
                }

                public void Receive(WebRtcSignal signal)
                {
                    _received.Invoke(signal);
                }
            }

            private class NoTunnels : IPeerTunnels
            {
                public IViewableDelegate<int> Incoming { get; } = new ViewableDelegate<int>();
                public IWebSocket Open(IReadOnlyLifetime lifetime, int remoteIndex) => throw new NotSupportedException();
            }
        }

        private class CaptureSocket : IWebSocket
        {
            public byte[] Frame { get; private set; }
            public IViewableDelegate<byte[]> Received { get; } = new ViewableDelegate<byte[]>();
            public IViewableDelegate<SocketCloseInfo> Closed { get; } = new ViewableDelegate<SocketCloseInfo>();

            public UniTask Connect() => UniTask.CompletedTask;

            public UniTask Send(ArraySegment<byte> bytes)
            {
                Frame ??= bytes.ToArray();
                return UniTask.CompletedTask;
            }

            public void Drop(string reason)
            {
            }
        }
    }
}
