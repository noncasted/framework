using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // Several channels of one WebRTC connection on libdatachannel: two peers inside the editor process negotiate through
    // a signaling pair and connect over host candidates (no STUN). The three channels of the constructor (records,
    // media, presence) open together, carry their own messages, and a large transfer of one does not hold back another.
    [NoAutoStaticsCleanup]
    public class WebRtcChannelsTests
    {
        private const int Records = 0;
        private const int Media = 1;
        private const int Presence = 2;

        private static readonly WebRtcChannelSpec[] Specs =
        {
            new("records", WebRtcChannelMode.ReliableOrdered),
            new("media", WebRtcChannelMode.ReliableOrdered),
            new("presence", WebRtcChannelMode.Unreliable)
        };

        // The open of every channel is one event of the connection, and a message comes on the channel it was sent to.
        [UnityTest]
        public IEnumerator Channels_OpenTogether_OnOpenOnce_MessagesInOwnChannel() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var offer = new RawPeer(Options());
                var answer = new RawPeer(Options());
                lifetime.Listen(offer.Dispose);
                lifetime.Listen(answer.Dispose);

                offer.Peer.CreateChannels();

                await Pump(offer, answer);

                Assert.AreEqual(1, offer.Opens, "the open of the channels of the offer");
                Assert.AreEqual(1, answer.Opens, "the open of the channels of the answer");

                for (var channel = 0; channel < Specs.Length; channel++)
                    offer.Peer.Send(channel, new[] { (byte)channel });

                await WaitUntil(() => answer.Messages.Count == Specs.Length);

                var messages = answer.Messages.ToArray();
                Assert.AreEqual(Specs.Length, messages.Length);

                foreach (var message in messages)
                    CollectionAssert.AreEqual(new[] { (byte)message.Channel }, message.Bytes, "a message came on another channel");
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // Every reliable channel keeps its own order, whatever goes on the other one.
        [UnityTest]
        public IEnumerator ReliableChannels_EveryMessageInOrder() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (offer, answer) = await ConnectPair(lifetime);
                var records = new List<byte[]>();
                var media = new List<byte[]>();

                answer.Channels[Records].Received.Advise(lifetime, records.Add);
                answer.Channels[Media].Received.Advise(lifetime, media.Add);

                for (var i = 0; i < 1000; i++)
                {
                    offer.Channels[Records].Send(BitConverter.GetBytes(i));
                    offer.Channels[Media].Send(BitConverter.GetBytes(-i));
                }

                await WaitUntil(() => records.Count == 1000 && media.Count == 1000);

                for (var i = 0; i < 1000; i++)
                {
                    Assert.AreEqual(i, BitConverter.ToInt32(records[i], 0));
                    Assert.AreEqual(-i, BitConverter.ToInt32(media[i], 0));
                }
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // The channels of one connection do not wait for each other: a small frame of records reaches the other side
        // while the megabytes of media are still going. The media goes as the media of the session sends it: a chunk at
        // a time, with the buffered amount as the limit (task 19), not one burst of the whole transfer.
        [UnityTest]
        public IEnumerator MediaTransfer_DoesNotHoldBackRecords() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (offer, answer) = await ConnectPair(lifetime);
                const int Chunk = 256 * 1024;
                const int Total = 20 * 1024 * 1024;
                const int Buffered = 4 * 1024 * 1024;
                var chunk = new byte[Chunk];
                var sent = 0;

                var mediaBytes = 0;
                var recordsArrived = false;
                var mediaAtRecords = -1;

                answer.Channels[Media].Received.Advise(lifetime, frame => mediaBytes += frame.Length);
                answer.Channels[Records].Received.Advise(lifetime, _ =>
                {
                    if (recordsArrived == true)
                        return;

                    recordsArrived = true;
                    mediaAtRecords = mediaBytes;
                });

                while (sent < Total)
                {
                    offer.Channels[Media].Send(chunk);
                    sent += Chunk;

                    if (sent == Chunk)
                        offer.Channels[Records].Send(new byte[] { 1 });

                    await WaitUntil(() => offer.Channels[Media].BufferedAmount < Buffered || recordsArrived);
                }

                await WaitUntil(() => recordsArrived);

                Assert.Less(mediaAtRecords, Total, "the records channel waited for the whole media transfer");
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // The bytes queued for sending are visible while the transfer goes and fall to zero when it is delivered.
        [UnityTest]
        public IEnumerator BufferedAmount_GrowsWithTheTransfer_FallsToZero() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (offer, answer) = await ConnectPair(lifetime);
                var large = new byte[20 * 1024 * 1024];
                var received = 0;

                answer.Channels[Media].Received.Advise(lifetime, frame => received += frame.Length);

                offer.Channels[Media].Send(large);

                Assert.Greater(offer.Channels[Media].BufferedAmount, 0, "the transfer is not buffered");

                await WaitUntil(() => offer.Channels[Media].BufferedAmount == 0 && received == large.Length);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // An unreliable channel carries messages as they are: no parts to join, a message larger than the channel throws
        // at the send.
        [UnityTest]
        public IEnumerator UnreliableChannel_ManyMessages_Whole_OversizeThrows() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (offer, answer) = await ConnectPair(lifetime);
                var received = new ConcurrentQueue<byte[]>();

                answer.Channels[Presence].Received.Advise(lifetime, received.Enqueue);

                for (var i = 0; i < 10000; i++)
                    offer.Channels[Presence].Send(BitConverter.GetBytes(i));

                await WaitUntil(() => received.Count > 0);
                await UniTask.Delay(500, DelayType.Realtime);

                Assert.Greater(received.Count, 0, "nothing came through the unreliable channel");

                foreach (var message in received.ToArray())
                    Assert.AreEqual(4, message.Length, "a message of an unreliable channel was joined from parts");

                Assert.Throws<InvalidOperationException>(() => offer.Channels[Presence].Send(new byte[1024 * 1024]));
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // The answering side accepts a channel by its label: a channel of a set the peer does not declare is the loss of
        // the whole connection, with the reason.
        [UnityTest]
        public IEnumerator Answer_UnknownLabel_ConnectionLostWithReason() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var offerOptions = Options(new WebRtcChannelSpec("extra", WebRtcChannelMode.ReliableOrdered));
                var answerOptions = Options(new WebRtcChannelSpec("expected", WebRtcChannelMode.ReliableOrdered));

                var offerEnd = new PeerHostTests.SignalingEnd();
                var answerEnd = new PeerHostTests.SignalingEnd();
                offerEnd.Connect(answerEnd);

                var offer = new WebRtcPeerLink(WebRtcPeerRole.Offer, offerEnd, offerOptions, lifetime);
                var answer = new WebRtcPeerLink(WebRtcPeerRole.Answer, answerEnd, answerOptions, lifetime);
                var offerConnect = offer.Connect();
                var answerConnect = answer.Connect();

                try
                {
                    await answerConnect.Timeout(TimeSpan.FromSeconds(10));
                    Assert.Fail("the answer accepted a channel it does not declare");
                }
                catch (WebRtcConnectFailedException exception)
                {
                    StringAssert.Contains("unknown channel label", exception.Message);
                }

                lifetime.Terminate();

                try
                {
                    await offerConnect.Timeout(TimeSpan.FromSeconds(5));
                }
                catch (Exception)
                {
                    // The offer loses the connection as well: its own reason is not the subject of the test.
                }
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // A Dispose while the channels are still opening: no crash of the library, no callback of the peer left in the
        // table of the callbacks.
        [UnityTest]
        public IEnumerator DisposeWhileOpening_NoCrash_NoCallbackLeft() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var link = new WebRtcPeerLink(WebRtcPeerRole.Offer, new SilentSignaling(), Options(), lifetime);
            var peer = (IWebRtcChannels)typeof(WebRtcPeerLink)
                                        .GetField("_peer", BindingFlags.Instance | BindingFlags.NonPublic)
                                        .GetValue(link);
            var key = (int)peer.GetType().GetField("_key", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(peer);
            var connect = link.Connect();

            await UniTask.DelayFrame(2);
            lifetime.Terminate();

            try
            {
                await connect.Timeout(TimeSpan.FromSeconds(5));
                Assert.Fail("Connect succeeded without an answer");
            }
            catch (OperationCanceledException)
            {
            }

            var callbacks = typeof(IWebRtcPeer).Assembly.GetType("Internal.NativeWebRtcCallbacks", true);
            var peers = (IDictionary)callbacks.GetField("Peers", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

            Assert.IsFalse(peers.Contains(key), "the callbacks of the disposed peer are still in the table");
        });

        private static WebRtcPeerOptions Options(params WebRtcChannelSpec[] specs)
        {
            return new WebRtcPeerOptions(Array.Empty<string>(), TimeSpan.FromSeconds(10),
                specs.Length > 0 ? specs : Specs);
        }

        private static async UniTask<(WebRtcPeerLink Offer, WebRtcPeerLink Answer)> ConnectPair(IReadOnlyLifetime lifetime)
        {
            var offerEnd = new PeerHostTests.SignalingEnd();
            var answerEnd = new PeerHostTests.SignalingEnd();
            offerEnd.Connect(answerEnd);

            var offer = new WebRtcPeerLink(WebRtcPeerRole.Offer, offerEnd, Options(), lifetime);
            var answer = new WebRtcPeerLink(WebRtcPeerRole.Answer, answerEnd, Options(), lifetime);

            await UniTask.WhenAll(offer.Connect(), answer.Connect());
            return (offer, answer);
        }

        // Drives the negotiation of two raw peers: the signals of one go to the other until both connections open.
        private static async UniTask Pump(RawPeer offer, RawPeer answer)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

            while ((offer.Opens == 0 || answer.Opens == 0) && DateTime.UtcNow < deadline)
            {
                while (offer.Signals.TryDequeue(out var signal) == true)
                    answer.Deliver(signal);

                while (answer.Signals.TryDequeue(out var signal) == true)
                    offer.Deliver(signal);

                // The threads of the library move the negotiation on between the passes.
                Thread.Sleep(20);
                await UniTask.Yield();
            }
        }

        private static async UniTask WaitUntil(Func<bool> condition)
        {
            var isTimedOut = await UniTask
                                   .WaitUntil(condition)
                                   .TimeoutWithoutException(TimeSpan.FromSeconds(20), DelayType.Realtime);

            Assert.IsFalse(isTimedOut, "The condition was not met in 20 s");
        }

        // The signaling of a connection whose offer goes nowhere.
        private sealed class SilentSignaling : IWebRtcSignaling
        {
            public IViewableDelegate<WebRtcSignal> Received { get; } = new ViewableDelegate<WebRtcSignal>();

            public void Send(WebRtcSignal signal)
            {
            }
        }

        // A platform peer of the test: the signals go through the queues, the messages and the opens are counted.
        private sealed class RawPeer : IWebRtcChannelEvents
        {
            public RawPeer(WebRtcPeerOptions options)
            {
                Peer = WebRtcPeers.CreateChannels(options, this);
            }

            public readonly IWebRtcChannels Peer;
            public readonly ConcurrentQueue<WebRtcSignal> Signals = new();
            public readonly ConcurrentQueue<(int Channel, byte[] Bytes)> Messages = new();

            public int Opens;
            public string Lost;

            public void Deliver(WebRtcSignal signal)
            {
                if (signal.Kind == WebRtcSignalKind.Description)
                    Peer.SetRemoteDescription(signal.Type, signal.Value);
                else
                    Peer.AddRemoteCandidate(signal.Value, signal.Mid);
            }

            public void Dispose()
            {
                Peer.Dispose();
            }

            public void OnLocalDescription(string type, string sdp) => Signals.Enqueue(WebRtcSignal.Description(type, sdp));
            public void OnLocalCandidate(string candidate, string mid) => Signals.Enqueue(WebRtcSignal.Candidate(candidate, mid));
            public void OnOpen() => Interlocked.Increment(ref Opens);
            public void OnMessage(int channel, byte[] bytes) => Messages.Enqueue((channel, bytes));
            public void OnLost(string reason) => Lost = reason;
        }
    }
}
