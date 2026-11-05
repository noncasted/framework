using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // The failures of a data channel in the order the platform may report them: Connect ends with the one exception
    // the caller falls back on, and an error of a send is a lost connection, not an exception in the code of the sender.
    [NoAutoStaticsCleanup]
    public class WebRtcPeerSocketFailureTests
    {
        private static readonly WebRtcPeerOptions Options = new(Array.Empty<string>(), TimeSpan.FromSeconds(10));

        [UnityTest]
        public IEnumerator OpenAndLostInOneFrame_ConnectFailsForTheFallback() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var socket = new WebRtcPeerSocket(WebRtcPeerRole.Answer, new PeerHostTests.SignalingEnd(), Options, lifetime);
                var events = Events(socket);
                var connect = socket.Connect();

                // The open comes from the test, not from the native channel: the peer has no channel to ask.
                Replace(socket, false);

                events.OnOpen();
                events.OnLost("closed right after the open");

                try
                {
                    await connect.Timeout(TimeSpan.FromSeconds(5));
                    Assert.Fail("Connect succeeded over a lost channel");
                }
                catch (WebRtcConnectFailedException exception)
                {
                    StringAssert.Contains("closed right after the open", exception.Message, "the real reason, not a timeout");
                }
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // The end of the lifetime is no failure of the channel: a fallback would open a tunnel on a dead lifetime.
        [UnityTest]
        public IEnumerator LifetimeEndsDuringConnect_NoFallback() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var socket = new WebRtcPeerSocket(WebRtcPeerRole.Answer, new PeerHostTests.SignalingEnd(), Options, lifetime);
            var connect = socket.Connect();

            lifetime.Terminate();

            try
            {
                await connect.Timeout(TimeSpan.FromSeconds(5));
                Assert.Fail("Connect succeeded after the end of its lifetime");
            }
            catch (OperationCanceledException)
            {
            }
        });

        // An error of the code above the socket does not stop its loop: the socket would never report anything again.
        [UnityTest]
        public IEnumerator HandlerThrows_NextEventsGoOn() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var signaling = new ThrowingSignaling();
            var socket = new WebRtcPeerSocket(WebRtcPeerRole.Answer, signaling, Options, lifetime);
            var events = Events(socket);

            try
            {
                LogAssert.ignoreFailingMessages = true;

                events.OnLocalCandidate("a", "0");
                await UniTask.DelayFrame(3);

                events.OnLocalCandidate("b", "0");
                await UniTask.DelayFrame(3);

                Assert.AreEqual(2, signaling.Sent);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                lifetime.Terminate();
            }
        });

        // The channel of a guest closed, its Lost is still in the queue: the send to it does not break the loop of the
        // caller over the other guests, the loss comes through Closed.
        [UnityTest]
        public IEnumerator SendThrows_NoException_ClosedFollows() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var offerEnd = new PeerHostTests.SignalingEnd();
                var answerEnd = new PeerHostTests.SignalingEnd();
                offerEnd.Connect(answerEnd);

                var offer = new WebRtcPeerSocket(WebRtcPeerRole.Offer, offerEnd, Options, lifetime);
                var answer = new WebRtcPeerSocket(WebRtcPeerRole.Answer, answerEnd, Options, lifetime);
                await UniTask.WhenAll(offer.Connect(), answer.Connect());

                var closed = false;
                offer.Closed.Advise(lifetime, _ => closed = true);

                Replace(offer, true);

                LogAssert.ignoreFailingMessages = true;
                Assert.DoesNotThrow(() => offer.Send(new byte[] { 1 }));

                await UniTask.WaitUntil(() => closed == true).Timeout(TimeSpan.FromSeconds(5));
            }
            finally
            {
                lifetime.Terminate();
                LogAssert.ignoreFailingMessages = false;
            }
        });

        // The library closed the connection, its state change is still in the queue: the signals of the other side that
        // come now are the failure of the channel the caller falls back on, not an exception in the code of the signaling.
        [UnityTest]
        public IEnumerator CandidateOnAClosedPeer_ConnectFailsForTheFallback() => UniTask.ToCoroutine(async () =>
        {
            await SignalOnAClosedPeer(WebRtcSignal.Description("offer", "sdp"), WebRtcSignal.Candidate("a", "0"));
        });

        [UnityTest]
        public IEnumerator DescriptionOnAClosedPeer_ConnectFailsForTheFallback() => UniTask.ToCoroutine(async () =>
        {
            await SignalOnAClosedPeer(WebRtcSignal.Description("offer", "sdp"));
        });

        // The library sets the description of the peer on its ICE agent first and keeps it for the check of the DTLS
        // certificate after that: with the connectivity checks of the peer already running, the handshake may reach the
        // check in between and fail. The peer has nothing to check before it gets the candidates of the offer.
        [UnityTest]
        public IEnumerator Offer_CandidatesGoAfterTheAnswerIsSet() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var signaling = new RecordingSignaling();
                var socket = new WebRtcPeerSocket(WebRtcPeerRole.Offer, signaling, Options, lifetime);
                var events = Events(socket);
                Replace(socket, false);

                events.OnLocalDescription("offer", "sdp");
                events.OnLocalCandidate("a", "0");
                events.OnLocalCandidate("b", "0");
                await UniTask.DelayFrame(3);

                CollectionAssert.AreEqual(new[] { "description offer" }, signaling.Sent);

                signaling.Receive(WebRtcSignal.Description("answer", "sdp"));
                events.OnLocalCandidate("c", "0");
                await UniTask.DelayFrame(3);

                CollectionAssert.AreEqual(new[] { "description offer", "candidate a", "candidate b", "candidate c" }, signaling.Sent);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        [UnityTest]
        public IEnumerator Answer_CandidatesGoAtOnce() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var signaling = new RecordingSignaling();
                var socket = new WebRtcPeerSocket(WebRtcPeerRole.Answer, signaling, Options, lifetime);
                var events = Events(socket);
                Replace(socket, false);

                events.OnLocalCandidate("a", "0");
                await UniTask.DelayFrame(3);

                CollectionAssert.AreEqual(new[] { "candidate a" }, signaling.Sent);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // A real error of the native library, written by its logger on its way out of the failed call. The failures of a
        // connection reach the code through the state of the peer: a log line alone is no error of the application.
        [UnityTest]
        public IEnumerator ErrorOfTheLibrary_WrittenAsWarning() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            LogType? written = null;

            void OnLog(string text, string stack, LogType type)
            {
                if (text.Contains("[libdatachannel]") == true)
                    written = type;
            }

            Application.logMessageReceived += OnLog;

            try
            {
                var peer = WebRtcPeers.Create(Options, new SilentWebRtcPeerEvents());
                lifetime.Listen(peer.Dispose);

                Assert.Throws<InvalidOperationException>(() => peer.AddRemoteCandidate("candidate", "0"));

                await UniTask.WaitUntil(() => written != null).Timeout(TimeSpan.FromSeconds(5));

                Assert.AreEqual(LogType.Warning, written);
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                lifetime.Terminate();
            }
        });

        private static async UniTask SignalOnAClosedPeer(params WebRtcSignal[] signals)
        {
            var lifetime = new Lifetime();

            try
            {
                var signaling = new RecordingSignaling();
                var socket = new WebRtcPeerSocket(WebRtcPeerRole.Answer, signaling, Options, lifetime);
                var connect = socket.Connect();

                Replace(socket, false, signals[signals.Length - 1].Kind);

                foreach (var signal in signals)
                    signaling.Receive(signal);

                try
                {
                    await connect.Timeout(TimeSpan.FromSeconds(5));
                    Assert.Fail("Connect succeeded over a closed peer");
                }
                catch (WebRtcConnectFailedException exception)
                {
                    StringAssert.Contains("the peer is closed", exception.Message, "the real reason, not a timeout");
                }
            }
            finally
            {
                lifetime.Terminate();
            }
        }

        // The events of the link of the socket: the test plays the platform peer through them.
        private static IWebRtcChannelEvents Events(WebRtcPeerSocket socket)
        {
            return (IWebRtcChannelEvents)Link(socket);
        }

        // The native peer stays under the replacement: it is disposed with it, its callbacks must not outlive the test.
        private static void Replace(WebRtcPeerSocket socket, bool throwsOnSend, WebRtcSignalKind? throwsOnSignal = null)
        {
            var link = Link(socket);
            var field = typeof(WebRtcPeerLink).GetField("_peer", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(link, new FakePeer((IWebRtcChannels)field.GetValue(link), throwsOnSend, throwsOnSignal));
        }

        private static WebRtcPeerLink Link(WebRtcPeerSocket socket)
        {
            var field = typeof(WebRtcPeerSocket).GetField("_link", BindingFlags.Instance | BindingFlags.NonPublic);
            return (WebRtcPeerLink)field.GetValue(socket);
        }

        [Test]
        public void IsSupported_OnlyPlatformsWithAPeer()
        {
            Assert.IsTrue(WebRtcPeers.IsSupported(RuntimePlatform.LinuxEditor));
            Assert.IsTrue(WebRtcPeers.IsSupported(RuntimePlatform.LinuxPlayer));
            Assert.IsTrue(WebRtcPeers.IsSupported(RuntimePlatform.WindowsPlayer));
            Assert.IsTrue(WebRtcPeers.IsSupported(RuntimePlatform.WebGLPlayer));

            Assert.IsFalse(WebRtcPeers.IsSupported(RuntimePlatform.OSXPlayer), "no native library: DllNotFoundException");
            Assert.IsFalse(WebRtcPeers.IsSupported(RuntimePlatform.Android));
            Assert.IsFalse(WebRtcPeers.IsSupported(RuntimePlatform.IPhonePlayer));
        }

        private class FakePeer : IWebRtcChannels
        {
            public FakePeer(IWebRtcChannels inner, bool throwsOnSend, WebRtcSignalKind? throwsOnSignal)
            {
                _inner = inner;
                _throwsOnSend = throwsOnSend;
                _throwsOnSignal = throwsOnSignal;
            }

            private readonly IWebRtcChannels _inner;
            private readonly bool _throwsOnSend;
            private readonly WebRtcSignalKind? _throwsOnSignal;

            public void CreateChannels()
            {
            }

            public int MaxMessageSize(int channel) => 16384;

            public int BufferedAmount(int channel) => 0;

            public void SetRemoteDescription(string type, string sdp)
            {
                ThrowOn(WebRtcSignalKind.Description);
            }

            public void AddRemoteCandidate(string candidate, string mid)
            {
                ThrowOn(WebRtcSignalKind.Candidate);
            }

            public void Send(int channel, byte[] message)
            {
                if (_throwsOnSend == true)
                    throw new InvalidOperationException("the channel is closed");
            }

            public void Dispose()
            {
                _inner.Dispose();
            }

            private void ThrowOn(WebRtcSignalKind kind)
            {
                if (_throwsOnSignal == kind)
                    throw new InvalidOperationException("the peer is closed");
            }
        }

        private class RecordingSignaling : IWebRtcSignaling
        {
            private readonly ViewableDelegate<WebRtcSignal> _received = new();
            private readonly List<string> _sent = new();

            public IReadOnlyList<string> Sent => _sent;
            public IViewableDelegate<WebRtcSignal> Received => _received;

            public void Send(WebRtcSignal signal)
            {
                _sent.Add(signal.ToString());
            }

            public void Receive(WebRtcSignal signal)
            {
                _received.Invoke(signal);
            }
        }

        private class ThrowingSignaling : IWebRtcSignaling
        {
            public int Sent { get; private set; }
            public IViewableDelegate<WebRtcSignal> Received { get; } = new ViewableDelegate<WebRtcSignal>();

            public void Send(WebRtcSignal signal)
            {
                Sent++;
                throw new InvalidOperationException("the signaling failed");
            }
        }
    }
}
