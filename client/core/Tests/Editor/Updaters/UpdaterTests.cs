using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.Tests
{
    // The speed of the updater component; its frame passes are tested in play mode (Internal.PlayMode.Tests).
    public class UpdaterTests
    {
        private GameObject _object;
        private Updater _updater;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Updater");
            _updater = _object.AddComponent<Updater>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void PauseTwice_NeedsTwoContinues()
        {
            _updater.SetSpeed(0.5f);
            _updater.Pause();
            _updater.Pause();
            _updater.Continue();

            Assert.AreEqual(0f, _updater.Speed.Value);

            _updater.Continue();

            Assert.AreEqual(0.5f, _updater.Speed.Value);
        }

        [Test]
        public void ContinueWithoutPause_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _updater.Continue());
        }

        [Test]
        public void SetSpeedWhilePaused_AppliesOnContinue()
        {
            _updater.Pause();
            _updater.SetSpeed(2f);

            Assert.AreEqual(0f, _updater.Speed.Value);

            _updater.Continue();

            Assert.AreEqual(2f, _updater.Speed.Value);
        }
    }
}
