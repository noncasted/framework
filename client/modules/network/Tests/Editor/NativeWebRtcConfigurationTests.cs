using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace Internal.Tests
{
    public class NativeWebRtcConfigurationTests
    {
        // The layout of rtcConfiguration (rtc.h of libdatachannel v0.24) on a 64-bit platform: the order and the sizes of
        // the fields of the struct must not change, the native side reads it by offset.
        [Test]
        public void Layout_MatchesTheNativeStruct()
        {
            var type = typeof(IWebRtcPeer).Assembly.GetType("Internal.NativeWebRtcApi+Configuration", true);

            Assert.AreEqual(new[] { 0, 8, 16, 24, 32, 36, 40, 41, 42, 43, 44, 46, 48, 52 }, Offsets(type));
            Assert.AreEqual(56, Marshal.SizeOf(type));
        }

        // The layout of rtcDataChannelInit (rtc.h of libdatachannel v0.24) on a 64-bit platform: rtcReliability is two C
        // bools and two unsigned ints, then the protocol pointer, the two flags and the stream. The native side reads it
        // by offset.
        [Test]
        public void DataChannelInit_Layout_MatchesTheNativeStruct()
        {
            var reliability = typeof(IWebRtcPeer).Assembly.GetType("Internal.NativeWebRtcApi+Reliability", true);
            var init = typeof(IWebRtcPeer).Assembly.GetType("Internal.NativeWebRtcApi+DataChannelInit", true);

            Assert.AreEqual(new[] { 0, 1, 4, 8 }, Offsets(reliability));
            Assert.AreEqual(12, Marshal.SizeOf(reliability));

            Assert.AreEqual(new[] { 0, 16, 24, 25, 26 }, Offsets(init));
            Assert.AreEqual(32, Marshal.SizeOf(init));
        }

        private static int[] Offsets(Type type)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .OrderBy(field => field.MetadataToken)
                       .Select(field => Marshal.OffsetOf(type, field.Name).ToInt32())
                       .ToArray();
        }
    }
}
