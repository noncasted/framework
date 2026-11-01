using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Internal.Tests
{
    // A wait on the updater ends when its callback throws (the error goes to its awaiter) and when its lifetime is
    // already over, whatever its time is.
    public class UpdaterFailureTests
    {
        [Test]
        public void Move_TargetDestroyed_WaitFaults()
        {
            var lifetime = new Lifetime();
            var updater = new ManualUpdater();
            var target = new GameObject("target");

            var wait = updater.Move(lifetime, target.transform, Vector3.one, 1f);
            UnityEngine.Object.DestroyImmediate(target);

            TickIsolated(updater);

            Assert.AreEqual(UniTaskStatus.Faulted, wait.Status, "the await hangs until the end of the scope");
            Assert.AreEqual(0, updater.UpdateCount, "the failing wait stays on the updater");

            lifetime.Terminate();
        }

        [Test]
        public void Progression_CallbackThrows_WaitFaults()
        {
            var lifetime = new Lifetime();
            var updater = new ManualUpdater();

            var wait = updater.Progression(lifetime, 1f, _ => throw new InvalidOperationException("callback failed"));

            TickIsolated(updater);

            Assert.AreEqual(UniTaskStatus.Faulted, wait.Status);
            Assert.AreEqual(0, updater.UpdateCount);

            lifetime.Terminate();
        }

        [Test]
        public void RunUpdateAction_CallbackThrows_WaitFaults()
        {
            var lifetime = new Lifetime();
            var updater = new ManualUpdater();

            var wait = updater.RunUpdateAction(lifetime, 1f, _ => throw new InvalidOperationException("callback failed"));

            TickIsolated(updater);

            Assert.AreEqual(UniTaskStatus.Faulted, wait.Status);
            Assert.AreEqual(0, updater.UpdateCount);

            lifetime.Terminate();
        }

        [Test]
        public void Tween_ZeroTime_LifetimeOver_IsCanceled_NoValue()
        {
            var lifetime = new Lifetime();
            lifetime.Terminate();
            var values = 0;

            var ease = new ManualUpdater().Tween(lifetime, 0f, EaseType.Linear, _ => values++);
            var curve = new ManualUpdater().Tween(lifetime, 0f, AnimationCurve.Linear(0f, 0f, 1f, 1f), _ => values++);

            Assert.AreEqual(UniTaskStatus.Canceled, ease.Status);
            Assert.AreEqual(UniTaskStatus.Canceled, curve.Status);
            Assert.AreEqual(0, values);
        }

        // The real updater isolates an updatable that throws: the test does the same.
        private static void TickIsolated(ManualUpdater updater)
        {
            try
            {
                updater.Tick(0.1f);
            }
            catch (Exception)
            {
            }
        }
    }
}
