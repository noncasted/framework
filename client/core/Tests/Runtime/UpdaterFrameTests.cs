using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Internal.Tests
{
    // The frame passes of the updater component.
    public class UpdaterFrameTests
    {
        private GameObject _object;
        private Updater _updater;
        private Lifetime _lifetime;
        private Action<Exception> _previousHandler;
        private List<Exception> _reported;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Updater");
            _updater = _object.AddComponent<Updater>();
            _lifetime = new Lifetime();

            _reported = new List<Exception>();
            _previousHandler = ReactiveExceptions.Handler;
            ReactiveExceptions.SetHandler(_reported.Add);
        }

        [TearDown]
        public void TearDown()
        {
            ReactiveExceptions.SetHandler(_previousHandler);
            _lifetime.Terminate();
            Object.Destroy(_object);
        }

        [UnityTest]
        public IEnumerator ThrowingUpdatable_TheOthersAndTheLaterPhasesRun_TheExceptionIsReported()
        {
            var after = new Counter();
            var post = new Counter();

            _updater.Add(_lifetime, (IUpdatable)new Counter { Throws = true });
            _updater.Add(_lifetime, (IUpdatable)after);
            _updater.Add(_lifetime, (IPostUpdatable)post);

            yield return null;
            yield return null;

            Assert.Greater(after.Updates, 0);
            Assert.Greater(post.PostUpdates, 0);
            Assert.Greater(_reported.Count, 0);
            Assert.AreEqual("Counter", _reported[0].Message);
        }

        [UnityTest]
        public IEnumerator UpdatableRemovedDuringThePass_IsNotUpdated()
        {
            var secondLifetime = _lifetime.Child();
            var second = new Counter();
            var first = new Counter { OnUpdated = () => secondLifetime.Terminate() };

            _updater.Add(_lifetime, (IUpdatable)first);
            _updater.Add(secondLifetime, (IUpdatable)second);

            yield return null;
            yield return null;

            Assert.Greater(first.Updates, 0);
            Assert.AreEqual(0, second.Updates);
        }

        private sealed class Counter : IUpdatable, IPostUpdatable
        {
            public bool Throws;
            public Action OnUpdated;
            public int Updates;
            public int PostUpdates;

            public void OnUpdate(float delta)
            {
                if (Throws == true)
                    throw new InvalidOperationException("Counter");

                Updates++;
                OnUpdated?.Invoke();
            }

            public void OnPostUpdate(float delta)
            {
                PostUpdates++;
            }
        }
    }
}
