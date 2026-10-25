using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // The UniTask waits of the client over the reactive primitives; the primitives themselves are tested in
    // common/Tests~ of the framework.
    public class ReactiveUniTaskTests
    {
        [UnityTest]
        public IEnumerator WaitInvoke_Invoked_CompletesAndUnsubscribesBoth() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new CountingLifetime();
            var source = new List<Action>();

            var wait = lifetime.WaitInvoke(source.Add, action => source.Remove(action));

            Assert.AreEqual(1, source.Count);
            Assert.AreEqual(1, lifetime.Count);

            source[0].Invoke();
            await wait;

            Assert.AreEqual(0, source.Count);
            Assert.AreEqual(0, lifetime.Count);
        });

        [UnityTest]
        public IEnumerator WaitInvoke_LifetimeEnds_CancelsAndUnsubscribes() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var source = new List<Action>();

            var wait = lifetime.WaitInvoke(source.Add, action => source.Remove(action));
            lifetime.Terminate();

            var isCancelled = false;

            try
            {
                await wait;
            }
            catch (OperationCanceledException)
            {
                isCancelled = true;
            }

            Assert.IsTrue(isCancelled);
            Assert.AreEqual(0, source.Count);
        });

        [UnityTest]
        public IEnumerator WaitTrue_CompletesOnTrue_LaterChangesAreIgnored() => UniTask.ToCoroutine(async () =>
        {
            var property = new ViewableProperty<bool>(false);
            var lifetime = new Lifetime();

            var wait = property.WaitTrue(lifetime);

            Assert.AreEqual(UniTaskStatus.Pending, wait.Status);

            property.Set(true);
            await wait;

            property.Set(false);
            property.Set(true);
        });

        [UnityTest]
        public IEnumerator WaitTrue_LifetimeEnds_IsCancelled() => UniTask.ToCoroutine(async () =>
        {
            var property = new ViewableProperty<bool>(false);
            var lifetime = new Lifetime();

            var wait = property.WaitTrue(lifetime);
            lifetime.Terminate();

            var isCancelled = false;

            try
            {
                await wait;
            }
            catch (OperationCanceledException)
            {
                isCancelled = true;
            }

            Assert.IsTrue(isCancelled);
        });

        private sealed class CountingLifetime : IReadOnlyLifetime
        {
            private readonly List<Action> _listeners = new();

            public int Count => _listeners.Count;
            public System.Threading.CancellationToken Token => System.Threading.CancellationToken.None;
            public bool IsTerminated => false;

            public void Listen(Action callback)
            {
                _listeners.Add(callback);
            }

            public void RemoveListener(Action callback)
            {
                _listeners.Remove(callback);
            }
        }
    }
}
