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

            var offsets = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              .OrderBy(field => field.MetadataToken)
                              .Select(field => Marshal.OffsetOf(type, field.Name).ToInt32())
                              .ToArray();

            Assert.AreEqual(new[] { 0, 8, 16, 24, 32, 36, 40, 41, 42, 43, 44, 46, 48, 52 }, offsets);
            Assert.AreEqual(56, Marshal.SizeOf(type));
        }
    }
}
