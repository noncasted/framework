using System.Collections.Generic;
using NUnit.Framework;
using static Internal.BehaviourNodes;

namespace Internal.Tests
{
    public class BehaviourTreeTests
    {
        [Test]
        public void Selector_AsksChildrenInOrder_StopsAtFirstNotFailed()
        {
            var log = new List<string>();

            var tree = new BehaviourTree(Selector(
                Leaf(log, "a", BehaviourStatus.Failure),
                Leaf(log, "b", BehaviourStatus.Success),
                Leaf(log, "c", BehaviourStatus.Success)));

            Assert.AreEqual(BehaviourStatus.Success, tree.Tick(0.1f));
            CollectionAssert.AreEqual(new[] { "a", "b" }, log);
        }

        [Test]
        public void Selector_EveryChildFailed_Fails()
        {
            var log = new List<string>();

            var tree = new BehaviourTree(Selector(
                Leaf(log, "a", BehaviourStatus.Failure),
                Leaf(log, "b", BehaviourStatus.Failure)));

            Assert.AreEqual(BehaviourStatus.Failure, tree.Tick(0.1f));
            CollectionAssert.AreEqual(new[] { "a", "b" }, log);
        }

        [Test]
        public void Sequence_AsksChildrenInOrder_StopsAtFirstFailed()
        {
            var log = new List<string>();

            var tree = new BehaviourTree(Sequence(
                Leaf(log, "a", BehaviourStatus.Success),
                Leaf(log, "b", BehaviourStatus.Failure),
                Leaf(log, "c", BehaviourStatus.Success)));

            Assert.AreEqual(BehaviourStatus.Failure, tree.Tick(0.1f));
            CollectionAssert.AreEqual(new[] { "a", "b" }, log);
        }

        [Test]
        public void Sequence_Running_GoesOnFromTheRunningChild()
        {
            var log = new List<string>();
            var status = BehaviourStatus.Running;

            var tree = new BehaviourTree(Sequence(
                Leaf(log, "a", BehaviourStatus.Success),
                Action(_ => {
                    log.Add("b");
                    return status;
                }),
                Leaf(log, "c", BehaviourStatus.Success)));

            Assert.AreEqual(BehaviourStatus.Running, tree.Tick(0.1f));
            Assert.AreEqual(BehaviourStatus.Running, tree.Tick(0.1f));

            status = BehaviourStatus.Success;

            Assert.AreEqual(BehaviourStatus.Success, tree.Tick(0.1f));
            CollectionAssert.AreEqual(new[] { "a", "b", "b", "b", "c" }, log);

            // Done: the next run starts from the first child again.
            log.Clear();
            tree.Tick(0.1f);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, log);
        }

        [Test]
        public void Selector_HigherChildInterruptsRunningLowerChild()
        {
            var isHigherReady = false;
            var resets = 0;

            var tree = new BehaviourTree(Selector(
                Sequence(Condition(() => isHigherReady), Action(_ => BehaviourStatus.Success)),
                Action(_ => BehaviourStatus.Running, () => resets++)));

            Assert.AreEqual(BehaviourStatus.Running, tree.Tick(0.1f));
            Assert.AreEqual(0, resets);

            isHigherReady = true;

            Assert.AreEqual(BehaviourStatus.Success, tree.Tick(0.1f));
            Assert.AreEqual(1, resets);

            // Already dropped: not reset twice.
            Assert.AreEqual(BehaviourStatus.Success, tree.Tick(0.1f));
            Assert.AreEqual(1, resets);
        }

        [Test]
        public void Action_FinishedByItself_IsNotReset()
        {
            var resets = 0;
            var status = BehaviourStatus.Running;
            var tree = new BehaviourTree(Action(_ => status, () => resets++));

            tree.Tick(0.1f);
            status = BehaviourStatus.Success;
            tree.Tick(0.1f);
            tree.Reset();

            Assert.AreEqual(0, resets);
        }

        [Test]
        public void Inverter_SwapsSuccessAndFailure_KeepsRunning()
        {
            Assert.AreEqual(BehaviourStatus.Failure, Inverter(Condition(() => true)).Tick(0f));
            Assert.AreEqual(BehaviourStatus.Success, Inverter(Condition(() => false)).Tick(0f));
            Assert.AreEqual(BehaviourStatus.Running, Inverter(Action(_ => BehaviourStatus.Running)).Tick(0f));
        }

        [Test]
        public void Cooldown_ClosesChildForDurationAfterItIsDone()
        {
            var runs = 0;

            var tree = new BehaviourTree(Cooldown(1f, Action(_ => {
                runs++;
                return BehaviourStatus.Success;
            })));

            Assert.AreEqual(BehaviourStatus.Success, tree.Tick(0.25f));
            Assert.AreEqual(BehaviourStatus.Failure, tree.Tick(0.5f));
            Assert.AreEqual(BehaviourStatus.Failure, tree.Tick(0.25f));
            Assert.AreEqual(1, runs);

            // 1.25 on the clock: a second has passed since the run at 0.25.
            Assert.AreEqual(BehaviourStatus.Success, tree.Tick(0.25f));
            Assert.AreEqual(2, runs);
        }

        [Test]
        public void Cooldown_RunningChild_HoldsItOpen_TimerStartsWhenDone()
        {
            var status = BehaviourStatus.Running;
            var tree = new BehaviourTree(Cooldown(1f, Action(_ => status)));

            Assert.AreEqual(BehaviourStatus.Running, tree.Tick(1f));
            Assert.AreEqual(BehaviourStatus.Running, tree.Tick(1f));

            status = BehaviourStatus.Success;

            Assert.AreEqual(BehaviourStatus.Success, tree.Tick(1f));
            Assert.AreEqual(BehaviourStatus.Failure, tree.Tick(0.5f));
            Assert.AreEqual(BehaviourStatus.Success, tree.Tick(0.5f));
        }

        [Test]
        public void Cooldown_Delay_ClosesTheFirstRun()
        {
            var tree = new BehaviourTree(Cooldown(1f, Action(_ => BehaviourStatus.Success), 0.5f));

            Assert.AreEqual(BehaviourStatus.Failure, tree.Tick(0.25f));
            Assert.AreEqual(BehaviourStatus.Success, tree.Tick(0.25f));
        }

        [Test]
        public void Cooldown_InterruptedRun_Counts_TimerOutlivesReset()
        {
            var tree = new BehaviourTree(Cooldown(1f, Action(_ => BehaviourStatus.Running)));

            Assert.AreEqual(BehaviourStatus.Running, tree.Tick(0.5f));
            tree.Reset();

            Assert.AreEqual(BehaviourStatus.Failure, tree.Tick(0.5f));
            Assert.AreEqual(BehaviourStatus.Running, tree.Tick(0.5f));
        }

        [Test]
        public void Timeout_ResetsChildRunningTooLong_AndFails()
        {
            var resets = 0;
            var tree = new BehaviourTree(Timeout(1f, Action(_ => BehaviourStatus.Running, () => resets++)));

            Assert.AreEqual(BehaviourStatus.Running, tree.Tick(0.5f));
            Assert.AreEqual(BehaviourStatus.Running, tree.Tick(0.4f));
            Assert.AreEqual(0, resets);

            Assert.AreEqual(BehaviourStatus.Failure, tree.Tick(0.6f));
            Assert.AreEqual(1, resets);

            // A new run counts its own time.
            Assert.AreEqual(BehaviourStatus.Running, tree.Tick(0.5f));
        }

        [Test]
        public void Tree_ClockIsTheSumOfDeltas()
        {
            var seen = 0f;

            var tree = new BehaviourTree(Action(time => {
                seen = time;
                return BehaviourStatus.Success;
            }));

            tree.Tick(0.5f);
            tree.Tick(0.25f);

            Assert.AreEqual(0.75f, seen, 0.0001f);
            Assert.AreEqual(0.75f, tree.Time, 0.0001f);
        }

        private static IBehaviourNode Leaf(List<string> log, string name, BehaviourStatus status)
        {
            return Action(_ => {
                log.Add(name);
                return status;
            });
        }
    }
}
