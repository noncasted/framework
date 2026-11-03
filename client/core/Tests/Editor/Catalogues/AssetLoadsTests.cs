using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Internal.Tests
{
    // Every load of a catalogue group goes through AssetLoads: a tool sees its start, its end and a failure with the error.
    public class AssetLoadsTests
    {
        private Lifetime _lifetime;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();
            _log = new List<string>();

            AssetLoads.Started.Advise(_lifetime, load => _log.Add($"start {load.Key}"));
            AssetLoads.Ended.Advise(_lifetime, load => _log.Add($"{(load.Error == null ? "end" : "fail")} {load.Key}"));
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
        }

        [Test]
        public void ALoadOfAGroup_IsRunningUntilItEnds()
        {
            var group = new AssetGroupTests.TestGroup();

            group.Retain().NoAwait();

            CollectionAssert.AreEqual(new[] { "start Group: TestGroup" }, _log);
            Assert.AreEqual(1, AssetLoads.Running.Count(load => load.Key == "Group: TestGroup"));

            group.CompleteLoad();

            CollectionAssert.AreEqual(new[] { "start Group: TestGroup", "end Group: TestGroup" }, _log);
            Assert.IsFalse(AssetLoads.Running.Any(load => load.Key == "Group: TestGroup"));

            group.Release();
        }

        [Test]
        public void AFailedLoad_EndsWithItsError()
        {
            var group = new AssetGroupTests.TestGroup();
            AssetLoad ended = null;
            AssetLoads.Ended.Advise(_lifetime, load => ended = load);

            var failed = group.Retain();
            group.FailLoad();

            Assert.Throws<InvalidOperationException>(() => failed.GetAwaiter().GetResult());

            CollectionAssert.AreEqual(new[] { "start Group: TestGroup", "fail Group: TestGroup" }, _log);
            Assert.IsInstanceOf<InvalidOperationException>(ended.Error);
            Assert.IsTrue(ended.IsOver);
            Assert.GreaterOrEqual(ended.ElapsedMs, 0d);

            group.Release();
        }

        [Test]
        public void ARetainOfALoadingGroup_StartsNoSecondLoad()
        {
            var group = new AssetGroupTests.TestGroup();

            group.Retain().NoAwait();
            group.Retain().NoAwait();
            group.CompleteLoad();

            Assert.AreEqual(1, _log.Count(line => line.StartsWith("start")));

            group.Release();
            group.Release();
        }

        [Test]
        public void ALoadEndedTwice_Throws()
        {
            var load = AssetLoads.Begin("Scene");

            load.Finish("Scene: Run");

            Assert.AreEqual("Scene: Run", load.Key);
            Assert.Throws<InvalidOperationException>(() => load.Finish());
        }
    }
}
