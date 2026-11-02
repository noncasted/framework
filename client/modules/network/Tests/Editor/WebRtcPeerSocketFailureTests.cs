using System;
using System.Collections;
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
                var events = (IWebRtcPeerEvents)socket;
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
            var events = (IWebRtcPeerEvents)socket;

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

        // The native peer stays under the replacement: it is disposed with it, its callbacks must not outlive the test.
        private static void Replace(WebRtcPeerSocket socket, bool throwsOnSend)
        {
            var field = typeof(WebRtcPeerSocket).GetField("_peer", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(socket, new FakePeer((IWebRtcPeer)field.GetValue(socket), throwsOnSend));
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

        private class FakePeer : IWebRtcPeer
        {
            public FakePeer(IWebRtcPeer inner, bool throwsOnSend)
            {
                _inner = inner;
                _throwsOnSend = throwsOnSend;
            }

            private readonly IWebRtcPeer _inner;
            private readonly bool _throwsOnSend;

            public int MaxMessageSize => 16384;

            public void CreateChannel(string label)
            {
            }

            public void SetRemoteDescription(string type, string sdp)
            {
            }

            public void AddRemoteCandidate(string candidate, string mid)
            {
            }

            public void Send(byte[] message)
            {
                if (_throwsOnSend == true)
                    throw new InvalidOperationException("the channel is closed");
            }

            public void Dispose()
            {
                _inner.Dispose();
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
