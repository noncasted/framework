using System;
using NUnit.Framework;

namespace Internal.Tests
{
    // A target leaves the update list when its lifetime ends (the list removes its handle by value): the removal
    // allocates nothing, so a scope with many short lived targets (shots, bodies, effects) makes no garbage by it.
    public class UpdateTargetsAllocationTests
    {
        private const int Passes = 1000;

        [Test]
        public void RemoveHandle_AllocatesNothing()
        {
            var list = new ModifiableList<UpdateTargetHandle<IUpdatable>>();
            var lifetime = new Lifetime();
            var handles = new UpdateTargetHandle<IUpdatable>[8];

            for (var i = 0; i < handles.Length; i++)
            {
                handles[i] = new UpdateTargetHandle<IUpdatable>(new Target(), lifetime);
                list.Add(handles[i]);
            }

            void Pass()
            {
                // Removed during a pass too: the list defers it to the end of the pass.
                foreach (var _ in list)
                {
                    list.Remove(handles[3]);
                    list.Add(handles[3]);
                    break;
                }

                list.Remove(handles[5]);
                list.Add(handles[5]);
            }

            // Warmed up: the lists grow and the comparers are made on the first passes.
            for (var i = 0; i < 10; i++)
                Pass();

            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var i = 0; i < Passes; i++)
                Pass();

            // Below a byte a pass: nothing per pass (a one-time allocation of the runtime aside, 48 B seen).
            Assert.Less(GC.GetAllocatedBytesForCurrentThread() - before, Passes);
            Assert.AreEqual(handles.Length, list.Count);
        }

        [Test]
        public void Handles_EqualByTargetAndLifetime()
        {
            var target = new Target();
            var lifetime = new Lifetime();

            Assert.IsTrue(new UpdateTargetHandle<IUpdatable>(target, lifetime)
                              .Equals(new UpdateTargetHandle<IUpdatable>(target, lifetime)));
            Assert.IsFalse(new UpdateTargetHandle<IUpdatable>(target, lifetime)
                               .Equals(new UpdateTargetHandle<IUpdatable>(target, new Lifetime())));
            Assert.IsFalse(new UpdateTargetHandle<IUpdatable>(target, lifetime)
                               .Equals(new UpdateTargetHandle<IUpdatable>(new Target(), lifetime)));
        }

        private class Target : IUpdatable
        {
            public void OnUpdate(float delta)
            {
            }
        }
    }
}
