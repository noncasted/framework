using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Internal.Tests
{
    public class NativeWebRtcPluginTests
    {
        private const string CommittedPlugin = "/Runtime/Plugins/libdatachannel/linux-x64/libframework_datachannel.so";

        // The tests of WebRTC must run on the library the players ship, not on one that happens to be in the system: it
        // has another TLS backend. Reads the modules of the process, so it has a meaning on Linux only.
        [Test]
#if !UNITY_EDITOR_LINUX
        [Ignore("/proc/self/maps exists on Linux only")]
#endif
        public void LoadedLibrary_IsTheCommittedPlugin()
        {
            typeof(IWebRtcPeer).Assembly.GetType("Internal.NativeWebRtcLibrary", true)
                               .GetMethod("Initialize", BindingFlags.Static | BindingFlags.Public)
                               .Invoke(null, null);

            var modules = File.ReadAllLines("/proc/self/maps")
                              .Select(line => line.Split(' ').Last())
                              .Where(path => Path.GetFileName(path).Contains("datachannel"))
                              .Distinct()
                              .ToArray();

            Assert.IsNotEmpty(modules, "libdatachannel is not loaded");

            foreach (var module in modules)
                Assert.That(module, Does.EndWith(CommittedPlugin));
        }
    }
}
