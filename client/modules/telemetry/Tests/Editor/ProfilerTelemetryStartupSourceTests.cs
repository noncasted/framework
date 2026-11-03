using System.IO;
using System.Linq;
using System.Reflection;
using Internal;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // Over the real GameProfiler: only the first trace of the run reaches the module.
    public class ProfilerTelemetryStartupSourceTests
    {
        private static readonly PropertyInfo _startup = typeof(GameProfiler).GetProperty(nameof(GameProfiler.Startup));

        // What the finishes of a test wrote: the folder of the traces stays as it was.
        private string[] _traces;

        [SetUp]
        public void RememberTraces()
        {
            _traces = ProfilerTraceStorage.List().ToArray();
        }

        [TearDown]
        public void DeleteNewTraces()
        {
            foreach (var path in ProfilerTraceStorage.List().Except(_traces))
                File.Delete(path);

            if (Directory.Exists(ProfilerTraceStorage.Directory) && Directory.GetFileSystemEntries(ProfilerTraceStorage.Directory).Length == 0)
                Directory.Delete(ProfilerTraceStorage.Directory);
        }

        [Test]
        public void NoTrace_NothingToRead()
        {
            _startup.SetValue(null, null);

            var source = new ProfilerTelemetryStartupSource();

            Assert.IsNull(source.ReadFinal());
            Assert.IsNull(source.ReadSnapshot());
        }

        [Test]
        public void OnlyTheFirstTrace_ReachesTheModule()
        {
            _startup.SetValue(null, null);

            var source = new ProfilerTelemetryStartupSource();
            var lifetime = new Lifetime();
            var finals = 0;

            source.ListenFinal(lifetime, () => finals++);

            GameProfiler.Begin("Startup");
            GameProfiler.Scope("Open");

            Assert.IsNull(source.ReadFinal());
            Assert.IsTrue(source.ReadSnapshot().Spans[0].Unfinished);

            GameProfiler.Finish();

            Assert.AreEqual(1, finals);
            Assert.AreEqual("Startup", source.ReadFinal().Name);

            GameProfiler.Begin("GamePlay");
            GameProfiler.Finish();

            Assert.AreEqual(1, finals);
            Assert.AreEqual("Startup", source.ReadFinal().Name);

            lifetime.Terminate();
        }

        [Test]
        public void EndOfTheLifetime_NoCall()
        {
            _startup.SetValue(null, null);

            var source = new ProfilerTelemetryStartupSource();
            var lifetime = new Lifetime();
            var finals = 0;

            source.ListenFinal(lifetime, () => finals++);
            lifetime.Terminate();

            GameProfiler.Begin("Startup");
            GameProfiler.Finish();

            Assert.AreEqual(0, finals);
        }
    }
}
