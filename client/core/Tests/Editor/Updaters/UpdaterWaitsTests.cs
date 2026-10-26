using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Internal.Tests
{
    // The waits over the time of an updater: they count its deltas and end with cancellation when the lifetime ends.
    public class UpdaterWaitsTests
    {
        [Test]
        public void RunDelay_EndsAfterTheTimeOfTheUpdater()
        {
            var lifetime = new Lifetime();
            var updater = new ManualUpdater();

            var wait = updater.RunDelay(lifetime, 0.25f);

            updater.Tick(0.1f);
            updater.Tick(0.1f);

            Assert.AreEqual(UniTaskStatus.Pending, wait.Status);

            updater.Tick(0.1f);
            updater.Tick(0.1f);

            Assert.AreEqual(UniTaskStatus.Succeeded, wait.Status);
            Assert.AreEqual(0, updater.UpdateCount);

            lifetime.Terminate();
        }

        [Test]
        public void RunDelay_LifetimeEnds_IsCanceled()
        {
            var lifetime = new Lifetime();
            var updater = new ManualUpdater();

            var wait = updater.RunDelay(lifetime, 1f);
            lifetime.Terminate();

            Assert.AreEqual(UniTaskStatus.Canceled, wait.Status);
        }

        [Test]
        public void RunUpdateAction_LifetimeEnds_IsCanceled()
        {
            var lifetime = new Lifetime();
            var updater = new ManualUpdater();

            var wait = updater.RunUpdateAction(lifetime, _ => { });
            lifetime.Terminate();

            Assert.AreEqual(UniTaskStatus.Canceled, wait.Status);
        }

        [Test]
        public void RunUpdateActionForTime_CountsTheDeltasOfTheUpdater()
        {
            var lifetime = new Lifetime();
            var updater = new ManualUpdater();
            var deltas = new List<float>();

            var wait = updater.RunUpdateAction(lifetime, 0.25f, deltas.Add);

            // A paused updater does not move the time of the action.
            updater.Pause();
            updater.Tick(1f);
            updater.Continue();

            Assert.AreEqual(UniTaskStatus.Pending, wait.Status);

            for (var i = 0; i < 4; i++)
                updater.Tick(0.1f);

            Assert.AreEqual(UniTaskStatus.Succeeded, wait.Status);
            Assert.AreEqual(4, deltas.Count);

            lifetime.Terminate();
        }

        [Test]
        public void Progression_LifetimeEnds_IsCanceled()
        {
            var lifetime = new Lifetime();
            var updater = new ManualUpdater();

            var wait = updater.Progression(lifetime, 1f, _ => { });
            lifetime.Terminate();

            Assert.AreEqual(UniTaskStatus.Canceled, wait.Status);
        }

        [Test]
        public void Progression_ZeroTime_CompletesWithFullProgress()
        {
            var lifetime = new Lifetime();
            var updater = new ManualUpdater();
            var progress = new List<float>();

            var wait = updater.Progression(lifetime, 0f, progress.Add);

            updater.Pause();
            updater.Tick(0.1f);

            Assert.AreEqual(UniTaskStatus.Succeeded, wait.Status);
            CollectionAssert.AreEqual(new[] { 1f }, progress);

            lifetime.Terminate();
        }

        [Test]
        public void CurveProgression_ZeroTime_CompletesWithTheEndOfTheCurve()
        {
            var lifetime = new Lifetime();
            var updater = new ManualUpdater();
            var values = new List<float>();

            var wait = updater.CurveProgression(lifetime, AnimationCurve.Linear(0f, 0f, 1f, 2f), 0f, values.Add);

            updater.Pause();
            updater.Tick(0.1f);

            Assert.AreEqual(UniTaskStatus.Succeeded, wait.Status);
            CollectionAssert.AreEqual(new[] { 2f }, values);

            lifetime.Terminate();
        }

        [Test]
        public void Tween_LifetimeEnds_IsCanceled()
        {
            var lifetime = new Lifetime();
            var updater = new ManualUpdater();

            var wait = updater.Tween(lifetime, 1f, EaseType.Linear, _ => { });
            lifetime.Terminate();

            Assert.AreEqual(UniTaskStatus.Canceled, wait.Status);
        }

        [Test]
        public void CurveInstance_ZeroTime_IsFinishedAtOnce()
        {
            var curve = new Curve(0f, AnimationCurve.Linear(0f, 0f, 1f, 2f));
            var instance = curve.CreateInstance();

            var value = instance.StepForward(0f);

            Assert.AreEqual(2f, value);
            Assert.AreEqual(1f, instance.Progress);
            Assert.IsTrue(instance.IsFinished);

            // Back on a curve without length is its start.
            Assert.AreEqual(0f, instance.StepBack(0f));
            Assert.AreEqual(0f, instance.Progress);
        }
    }
}
