using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Global.Inputs;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Global.UI.Tests
{
    // UIStateMachine: the stack head after the stacked states close in any order; the visibility of a state hidden
    // both by its parent and by a state stacked over it; the exit of a processed state whose action throws.
    public class UIStateMachineTests
    {
        // A over Base, B over A: A exits (B with it, a child of A). The head is Base again, not the removed A.
        [Test]
        public void StackChildExits_WithItsStackedChild_HeadIsBase()
        {
            var (machine, _, lifetime) = Create();

            var a = machine.CreateStackChild(lifetime, machine.Base, new State());
            machine.CreateStackChild(lifetime, machine.StackHead, new State());

            a.Exit();

            Assert.AreSame(machine.Base, machine.StackHead);
            Assert.DoesNotThrow(() => machine.CreateChild(lifetime, machine.StackHead, new State()));
            lifetime.Terminate();
        }

        // A over Base, then B over Base: A exits first, then B. The head is Base, not A that B covered at its start.
        [Test]
        public void StackChildrenExitOutOfOrder_HeadIsBase()
        {
            var (machine, _, lifetime) = Create();

            var a = machine.CreateStackChild(lifetime, machine.Base, new State());
            var b = machine.CreateStackChild(lifetime, machine.Base, new State());

            a.Exit();
            Assert.AreSame(b.State, machine.StackHead);

            b.Exit();

            Assert.AreSame(machine.Base, machine.StackHead);
            Assert.DoesNotThrow(() => machine.CreateChild(lifetime, machine.StackHead, new State()));
            lifetime.Terminate();
        }

        // W (game input off) is a child of P; a dialog D is stacked over W. P is hidden and shown again (X stacked over
        // it and closed): W stays hidden under D, the game input stays as D has it.
        [Test]
        public void ChildCoveredByStack_ParentShownAgain_StaysHidden()
        {
            var (machine, storage, lifetime) = Create();

            var p = machine.CreateChild(lifetime, machine.Base, new State());
            var w = machine.CreateChild(lifetime, p.State, new State(UIConstraints.Game));
            machine.CreateStackChild(lifetime, w.State, new State());

            Assert.IsFalse(w.IsVisible.Value);
            Assert.IsTrue(storage[InputConstraints.Game]);

            var x = machine.CreateStackChild(lifetime, p.State, new State());
            x.Exit();

            Assert.IsTrue(p.IsVisible.Value);
            Assert.IsFalse(w.IsVisible.Value);
            Assert.IsTrue(storage[InputConstraints.Game]);
            lifetime.Terminate();
        }

        // Two states stacked over one: the end of the first does not show it while the second still covers it.
        [Test]
        public void TwoStacked_FirstExits_StaysHidden()
        {
            var (machine, _, lifetime) = Create();

            var w = machine.CreateChild(lifetime, machine.Base, new State());
            var first = machine.CreateStackChild(lifetime, w.State, new State());
            machine.CreateStackChild(lifetime, w.State, new State());

            first.Exit();

            Assert.IsFalse(w.IsVisible.Value);
            lifetime.Terminate();
        }

        // The action of a processed window throws: the window exits anyway, its constraints go.
        [UnityTest]
        public IEnumerator ProcessChild_ActionThrows_StateExits() => UniTask.ToCoroutine(async () =>
        {
            var (machine, storage, lifetime) = Create();
            IUIStateHandle handle = null;

            try
            {
                await machine.ProcessChild(lifetime, machine.Base, new State(UIConstraints.Game), entered => {
                    handle = entered;
                    throw new InvalidOperationException("the window failed");
                });
            }
            catch (InvalidOperationException)
            {
            }

            Assert.IsTrue(handle.InnerLifetime.IsTerminated);
            Assert.IsTrue(storage[InputConstraints.Game]);
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator ProcessStack_ActionThrows_StateExits() => UniTask.ToCoroutine(async () =>
        {
            var (machine, storage, lifetime) = Create();

            try
            {
                await machine.ProcessStack<int>(lifetime, machine.Base, new State(UIConstraints.Game),
                    _ => throw new InvalidOperationException("the window failed"));
            }
            catch (InvalidOperationException)
            {
            }

            Assert.IsTrue(storage[InputConstraints.Game]);
            Assert.AreSame(machine.Base, machine.StackHead);
            lifetime.Terminate();
        });

        private static (UIStateMachine, InputConstraintsStorage, Internal.Lifetime) Create()
        {
            var lifetime = new Internal.Lifetime();
            var storage = new InputConstraintsStorage();
            return (new UIStateMachine(storage, lifetime), storage, lifetime);
        }

        private class State : IUIState
        {
            public State()
            {
                Constraints = UIConstraints.Empty;
            }

            public State(IUIConstraints constraints)
            {
                Constraints = constraints;
            }

            public IUIConstraints Constraints { get; }
        }
    }
}
