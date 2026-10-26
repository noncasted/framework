using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    public class StateMachineTests
    {
        [Test]
        public void CreateHandle_TerminatesPreviousStateLifetime()
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var first = new TestState("First", 0);
            var second = new TestState("Second", 0);

            var firstHandle = machine.CreateHandle(first);
            var secondHandle = machine.CreateHandle(second);

            Assert.IsTrue(firstHandle.Lifetime.IsTerminated);
            Assert.IsFalse(secondHandle.Lifetime.IsTerminated);
            Assert.AreSame(second, machine.Current);

            lifetime.Terminate();
        }

        [Test]
        public void Exit_ForeignState_Throws()
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var current = new TestState("Current", 0);
            var foreign = new TestState("Foreign", 0);

            machine.CreateHandle(current);

            Assert.Throws<InvalidOperationException>(() => machine.Exit(foreign));
            Assert.AreSame(current, machine.Current);

            lifetime.Terminate();
        }

        [Test]
        public void CreateHandle_SameDefinitionAsCurrent_Throws()
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var state = new TestState("State", 0);

            machine.CreateHandle(state);

            Assert.Throws<InvalidOperationException>(() => machine.CreateHandle(state));

            lifetime.Terminate();
        }

        [Test]
        public void CreateHandle_EntityLifetimeTerminated_Throws()
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            lifetime.Terminate();

            Assert.Throws<InvalidOperationException>(() => machine.CreateHandle(new TestState("State", 0)));
        }

        [Test]
        public void RestartHandle_Current_TerminatesPreviousWithoutExited()
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var exitedCount = 0;
            machine.Exited.Advise(lifetime, () => exitedCount++);

            var state = new TestState("State", 0);
            var first = machine.CreateHandle(state);
            var second = machine.RestartHandle(state);

            Assert.IsTrue(first.Lifetime.IsTerminated);
            Assert.IsFalse(second.Lifetime.IsTerminated);
            Assert.AreSame(state, machine.Current);
            Assert.AreEqual(0, exitedCount);

            lifetime.Terminate();
        }

        [Test]
        public void RestartHandle_NotCurrent_Throws()
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            machine.CreateHandle(new TestState("Current", 0));

            Assert.Throws<InvalidOperationException>(() => machine.RestartHandle(new TestState("Other", 0)));

            lifetime.Terminate();
        }

        [UnityTest]
        public IEnumerator PriorityQueue_SelectedButCurrentIsSameDefinition_NotEntered() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var state = new TestState("State", 0);
            var waiting = machine.TryCreateHandleFromPriorityQueue(state);

            // Entered directly while the queued copy waits: the queue must not enter it a second time.
            var direct = machine.CreateHandle(state);

            var (isSelected, handle) = await waiting;

            Assert.IsFalse(isSelected);
            Assert.IsNull(handle);
            Assert.IsFalse(direct.Lifetime.IsTerminated);

            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator PriorityQueue_HighestPriorityWins() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var low = new TestState("Low", 1);
            var high = new TestState("High", 2);

            var ((lowSelected, lowHandle), (highSelected, highHandle)) = await UniTask.WhenAll(
                machine.TryCreateHandleFromPriorityQueue(low),
                machine.TryCreateHandleFromPriorityQueue(high));

            Assert.IsFalse(lowSelected);
            Assert.IsNull(lowHandle);
            Assert.IsTrue(highSelected);
            Assert.IsNotNull(highHandle);
            Assert.AreSame(high, machine.Current);

            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator PriorityQueue_LifetimeTerminatedWhileWaiting_NotSelected() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var state = new TestState("State", 0);
            var waiting = machine.TryCreateHandleFromPriorityQueue(state);

            lifetime.Terminate();

            var (isSelected, handle) = await waiting;

            Assert.IsFalse(isSelected);
            Assert.IsNull(handle);
            Assert.IsNull(machine.Current);
        });

        [Test]
        public void CreateHandle_ListenerOfPreviousStateEntersAnother_OnlyOneStateRuns()
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var first = new TestState("First", 0);
            var second = new TestState("Second", 0);
            var third = new TestState("Third", 0);

            IStateHandle thirdHandle = null;
            var firstHandle = machine.CreateHandle(first);
            firstHandle.Lifetime.Listen(() => thirdHandle = machine.CreateHandle(third));

            var secondHandle = machine.CreateHandle(second);

            // The last entered state is the current one, the lifetime of every other one has ended.
            Assert.AreSame(third, machine.Current);
            Assert.IsFalse(thirdHandle.Lifetime.IsTerminated);
            Assert.IsTrue(secondHandle.Lifetime.IsTerminated);

            lifetime.Terminate();
        }

        [Test]
        public void Exit_ListenerOfTheStateEntersAnother_ItStaysCurrent()
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var first = new TestState("First", 0);
            var second = new TestState("Second", 0);

            IStateHandle secondHandle = null;
            var firstHandle = machine.CreateHandle(first);
            firstHandle.Lifetime.Listen(() => secondHandle = machine.CreateHandle(second));

            firstHandle.Exit();

            Assert.AreSame(second, machine.Current);
            Assert.IsFalse(secondHandle.Lifetime.IsTerminated);

            lifetime.Terminate();
        }

        [Test]
        public void Initialize_Running_EndsTheCurrentState()
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var handle = machine.CreateHandle(new TestState("State", 0));

            var nextLifetime = new Lifetime();
            machine.Initialize(nextLifetime);

            Assert.IsTrue(handle.Lifetime.IsTerminated);
            Assert.IsNull(machine.Current);

            lifetime.Terminate();
            nextLifetime.Terminate();
        }

        [UnityTest]
        public IEnumerator Initialize_WhileQueued_TheQueuedStateIsNotEntered() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var waiting = machine.TryCreateHandleFromPriorityQueue(new TestState("State", 0));

            var nextLifetime = new Lifetime();
            machine.Initialize(nextLifetime);

            var (isSelected, handle) = await waiting;

            Assert.IsFalse(isSelected);
            Assert.IsNull(handle);
            Assert.IsNull(machine.Current);

            // The queue works for the new run.
            var (isNextSelected, _) = await machine.TryCreateHandleFromPriorityQueue(new TestState("Next", 0));

            Assert.IsTrue(isNextSelected);

            lifetime.Terminate();
            nextLifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator PriorityQueue_SameStateTwice_SecondIsRefused() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var state = new TestState("State", 0);

            var first = machine.TryCreateHandleFromPriorityQueue(state);
            var second = machine.TryCreateHandleFromPriorityQueue(state);

            Assert.AreEqual(UniTaskStatus.Succeeded, second.Status);

            var (isSecondSelected, _) = await second;
            var (isFirstSelected, firstHandle) = await first;

            Assert.IsFalse(isSecondSelected);
            Assert.IsTrue(isFirstSelected);
            Assert.AreSame(state, machine.Current);
            Assert.IsFalse(firstHandle.Lifetime.IsTerminated);

            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator PriorityQueue_LaterRequest_GetsItsOwnRound() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var machine = new StateMachine();
            machine.Initialize(lifetime);

            var low = new TestState("Low", 1);
            var high = new TestState("High", 2);

            var (isLowSelected, lowHandle) = await machine.TryCreateHandleFromPriorityQueue(low);

            Assert.IsTrue(isLowSelected);

            // The current state does not hold the queue: a request of the next round interrupts it.
            var (isHighSelected, _) = await machine.TryCreateHandleFromPriorityQueue(high);

            Assert.IsTrue(isHighSelected);
            Assert.IsTrue(lowHandle.Lifetime.IsTerminated);
            Assert.AreSame(high, machine.Current);

            lifetime.Terminate();
        });

        private class TestState : IState
        {
            public TestState(string name, int priority)
            {
                Definition = new ObjectStateDefinition(Guid.NewGuid(), name);
                Priority = priority;
            }

            public IStateDefinition Definition { get; }
            public int Priority { get; }

            public bool IsAvailable() => true;
            public UniTask Enter() => UniTask.CompletedTask;
        }
    }
}
