using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // WebRtcPeerSocket on libdatachannel: two peers inside the editor process negotiate through a signaling pair and
    // connect over host candidates (no STUN). The channel carries frames in order, splits and joins the frames larger
    // than a message, and reports the loss of the other side through Closed.
    [NoAutoStaticsCleanup]
    public class WebRtcPeerSocketTests
    {
        private static readonly WebRtcPeerOptions Options = new(Array.Empty<string>(), TimeSpan.FromSeconds(10));

        [UnityTest]
        public IEnumerator Connect_Frames_ArriveInOrder() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (offer, answer) = await ConnectPair(lifetime);
                var received = new List<byte[]>();
                answer.Received.Advise(lifetime, received.Add);

                for (var i = 0; i < 1000; i++)
                    await offer.Send(BitConverter.GetBytes(i));

                await WaitUntil(() => received.Count == 1000);

                for (var i = 0; i < 1000; i++)
                    Assert.AreEqual(i, BitConverter.ToInt32(received[i], 0));
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        [UnityTest]
        public IEnumerator Send_LargeFrame_SplitAndJoined() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var (offer, answer) = await ConnectPair(lifetime);
                var received = new List<byte[]>();
                offer.Received.Advise(lifetime, received.Add);

                var large = new byte[1024 * 1024];
                new Random(7).NextBytes(large);

                await answer.Send(new byte[] { 1 });
                await answer.Send(large);
                await answer.Send(new byte[] { 2 });
                await answer.Send(Array.Empty<byte>());

                await WaitUntil(() => received.Count == 4);

                CollectionAssert.AreEqual(new byte[] { 1 }, received[0]);
                CollectionAssert.AreEqual(large, received[1]);
                CollectionAssert.AreEqual(new byte[] { 2 }, received[2]);
                Assert.AreEqual(0, received[3].Length);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        [UnityTest]
        public IEnumerator OtherSideTerminated_ClosedIsRaised() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var answerLifetime = lifetime.Child();
                var (offer, _) = await ConnectPair(lifetime, answerLifetime);
                var closes = new List<SocketCloseInfo>();
                offer.Closed.Advise(lifetime, closes.Add);

                answerLifetime.Terminate();

                await WaitUntil(() => closes.Count > 0);

                Assert.AreEqual(1, closes.Count);
                Assert.AreEqual(SocketCloseInfo.AbnormalClosure, closes[0].Code);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        [UnityTest]
        public IEnumerator Connect_NoAnswer_FailsInTimeout() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var signaling = new SignalingEnd();
                var options = new WebRtcPeerOptions(Array.Empty<string>(), TimeSpan.FromMilliseconds(500));
                var offer = new WebRtcPeerSocket(WebRtcPeerRole.Offer, signaling, options, lifetime);

                Exception exception = null;

                try
                {
                    await offer.Connect();
                }
                catch (Exception caught)
                {
                    exception = caught;
                }

                Assert.IsInstanceOf<WebRtcConnectFailedException>(exception);
                Assert.Greater(signaling.Sent, 0, "The offer was not sent to the signaling");
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        private static async UniTask<(WebRtcPeerSocket Offer, WebRtcPeerSocket Answer)> ConnectPair(
            IReadOnlyLifetime lifetime,
            IReadOnlyLifetime answerLifetime = null)
        {
            var offerEnd = new SignalingEnd();
            var answerEnd = new SignalingEnd();
            offerEnd.Other = answerEnd;
            answerEnd.Other = offerEnd;

            var offer = new WebRtcPeerSocket(WebRtcPeerRole.Offer, offerEnd, Options, lifetime);
            var answer = new WebRtcPeerSocket(WebRtcPeerRole.Answer, answerEnd, Options, answerLifetime ?? lifetime);

            var offerConnect = offer.Connect();
            var answerConnect = answer.Connect();
            await UniTask.WhenAll(offerConnect, answerConnect);

            return (offer, answer);
        }

        private static async UniTask WaitUntil(Func<bool> condition)
        {
            var isTimedOut = await UniTask
                                   .WaitUntil(condition)
                                   .TimeoutWithoutException(TimeSpan.FromSeconds(10), DelayType.Realtime);

            Assert.IsFalse(isTimedOut, "The condition was not met in 10 s");
        }

        private class SignalingEnd : IWebRtcSignaling
        {
            private readonly ViewableDelegate<WebRtcSignal> _received = new();

            public SignalingEnd Other;
            public int Sent;

            public IViewableDelegate<WebRtcSignal> Received => _received;

            public void Send(WebRtcSignal signal)
            {
                Sent++;
                Other?._received.Invoke(signal);
            }
        }
    }
}
