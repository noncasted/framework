using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    public class NativeWebRtcLateChannelTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly Assembly Network = typeof(IWebRtcPeer).Assembly;
        private static readonly Type Callbacks = Network.GetType("Internal.NativeWebRtcCallbacks", true);
        private static readonly Type Api = Network.GetType("Internal.NativeWebRtcApi", true);

        // The library hands over a new channel of the peer through the callback and keeps it until rtcDelete. A channel
        // that comes after the Dispose of its peer has no other owner: only the callback can delete it. A real channel
        // of another peer stands for it, rtcDelete fails on an id that is already deleted.
        [Test]
        public void ChannelOfDisposedPeer_IsDeleted()
        {
            var gone = Create();
            var key = Key(gone);
            gone.Dispose();

            WithForeignChannel(channel =>
            {
                HandOver(channel, key);

                Assert.Less(Delete(channel), 0, "the channel of a disposed peer is still alive");
            });
        }

        // The callback found the peer in the table, then Dispose ran to its end on the main thread, then SetupChannel goes
        // on: the order of a thread of the library preempted between the search and SetupChannel, without the timing. The
        // Dispose has already passed, so nobody else deletes the channel.
        [Test]
        public void ChannelSetUpAfterDispose_IsDeleted()
        {
            var gone = Create();
            gone.Dispose();

            WithForeignChannel(channel =>
            {
                gone.GetType().GetMethod("SetupChannel", Instance).Invoke(gone, new object[] { channel });

                Assert.Less(Delete(channel), 0, "the channel set up after the Dispose of its peer is still alive");
            });
        }

        // The peer keeps only its first channel: a second one has no owner but the code that rejects it.
        [Test]
        public void SecondChannel_IsDeleted()
        {
            var peer = Create();

            try
            {
                peer.CreateChannel("session");

                WithForeignChannel(channel =>
                {
                    LogAssert.Expect(LogType.Exception, new Regex("A second data channel"));

                    HandOver(channel, Key(peer));

                    Assert.Less(Delete(channel), 0, "the second channel is still alive");
                });
            }
            finally
            {
                peer.Dispose();
            }
        }

        [Test]
        public void Dispose_DeletesTheChannel()
        {
            var peer = Create();
            peer.CreateChannel("session");
            var channel = Channel(peer);

            peer.Dispose();

            Assert.Less(Delete(channel), 0, "the channel of the peer is still alive");
        }

        private static void WithForeignChannel(Action<int> test)
        {
            var other = Create();

            try
            {
                other.CreateChannel("session");
                test(Channel(other));
            }
            finally
            {
                other.Dispose();
            }
        }

        private static void HandOver(int channel, int key)
        {
            var callback = (Delegate)Callbacks.GetField("OnDataChannelCallback", BindingFlags.Static | BindingFlags.Public)
                                              .GetValue(null);
            callback.DynamicInvoke(0, channel, new IntPtr(key));
        }

        private static int Delete(int channel)
        {
            return (int)Api.GetMethod("Delete", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { channel });
        }

        private static int Key(IWebRtcPeer peer)
        {
            return (int)peer.GetType().GetField("_key", Instance).GetValue(peer);
        }

        private static int Channel(IWebRtcPeer peer)
        {
            return (int)peer.GetType().GetField("_channel", Instance).GetValue(peer);
        }

        private static IWebRtcPeer Create()
        {
            return WebRtcPeers.Create(new WebRtcPeerOptions(Array.Empty<string>(), TimeSpan.FromSeconds(10)), new SilentWebRtcPeerEvents());
        }
    }
}
