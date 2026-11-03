using System.Linq;
using Internal;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // IClientTelemetry.AddCollector: a collector of a scope below the telemetry, over the sessions of the real collection.
    public class TelemetryAddedCollectorsTests
    {
        private TelemetryFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = new TelemetryFixture();
        }

        private bool HasName(string name)
        {
            return Enumerable.Range(0, _fixture.Names.Count).Any(i => _fixture.Names.NameAt(i) == name);
        }

        [Test]
        public void Added_InOpenSession_SampledAndStarted()
        {
            var session = new Lifetime();
            var run = new Lifetime();
            var added = new LiveProbeCollector("run.enemies");

            _fixture.Telemetry.StartSession(session, "menu");
            _fixture.Telemetry.AddCollector(run, added);
            _fixture.Sampler.Sample();

            Assert.AreEqual(1, added.Starts);
            Assert.IsTrue(HasName("run.enemies"));
            session.Terminate();
        }

        [Test]
        public void Added_LifetimeEnds_NotSampledAndItsRunningEnds()
        {
            var session = new Lifetime();
            var run = new Lifetime();
            var added = new LiveProbeCollector("run.enemies");

            _fixture.Telemetry.StartSession(session, "run");
            _fixture.Telemetry.AddCollector(run, added);
            run.Terminate();
            _fixture.Sampler.Sample();

            Assert.IsTrue(added.Running.IsTerminated);
            Assert.AreEqual(0, added.Collects);
            session.Terminate();
        }

        [Test]
        public void Added_NextSession_StartedAgainWithAFreshLifetime()
        {
            var run = new Lifetime();
            var added = new LiveProbeCollector("run.enemies");
            var first = new Lifetime();

            _fixture.Telemetry.AddCollector(run, added);
            Assert.AreEqual(0, added.Starts);

            _fixture.Telemetry.StartSession(first, "run");
            var firstRunning = added.Running;
            first.Terminate();
            Assert.IsTrue(firstRunning.IsTerminated);

            var second = new Lifetime();
            _fixture.Telemetry.StartSession(second, "run");
            Assert.AreEqual(2, added.Starts);
            Assert.IsFalse(added.Running.IsTerminated);

            run.Terminate();
            Assert.IsTrue(added.Running.IsTerminated);
            second.Terminate();
        }

        [Test]
        public void Added_Throws_OthersStillSampled()
        {
            var session = new Lifetime();
            var run = new Lifetime();

            _fixture.Telemetry.StartSession(session, "run");
            _fixture.Telemetry.AddCollector(run, new ThrowingCollector());
            _fixture.Telemetry.AddCollector(run, new LiveProbeCollector("run.enemies"));

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            _fixture.Sampler.Sample();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            Assert.IsTrue(HasName("run.enemies"));
            session.Terminate();
        }

        [Test]
        public void Added_TerminatedLifetime_Ignored()
        {
            var session = new Lifetime();
            var run = new Lifetime();
            var added = new LiveProbeCollector("run.enemies");
            run.Terminate();

            _fixture.Telemetry.StartSession(session, "run");
            _fixture.Telemetry.AddCollector(run, added);
            _fixture.Sampler.Sample();

            Assert.AreEqual(0, added.Starts + added.Collects);
            session.Terminate();
        }

        [Test]
        public void Added_TelemetryOff_NeverCalled()
        {
            var fixture = new TelemetryFixture(enabledInEditor: false);
            var added = new LiveProbeCollector("run.enemies");

            fixture.Telemetry.AddCollector(new Lifetime(), added);
            fixture.Telemetry.StartSession(new Lifetime(), "run");
            fixture.Sampler.Sample();

            Assert.AreEqual(0, added.Starts + added.Collects);
        }
    }
}
