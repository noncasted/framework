using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.Tests
{
    public class PrefabPoolTests
    {
        private Lifetime _lifetime;
        private SpriteRenderer _template;
        private Transform _parent;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();

            var template = new GameObject("Template") { hideFlags = HideFlags.HideAndDontSave };
            _template = template.AddComponent<SpriteRenderer>();
            _template.sortingOrder = 7;

            _parent = new GameObject("Parent") { hideFlags = HideFlags.HideAndDontSave }.transform;
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
            Object.DestroyImmediate(_template.gameObject);
            Object.DestroyImmediate(_parent.gameObject);
        }

        [Test]
        public void Get_Empty_CreatesActiveInstanceUnderTheParent()
        {
            var pool = new PrefabPool<SpriteRenderer>(_lifetime, _template, _parent);

            var instance = pool.Get(_lifetime.Child());

            Assert.AreSame(_parent, instance.transform.parent);
            Assert.AreEqual("Template_1", instance.name);
            Assert.AreEqual(7, instance.sortingOrder);
            Assert.IsTrue(instance.gameObject.activeSelf);
            Assert.AreEqual(1, pool.Count);
            Assert.AreEqual(0, pool.FreeCount);
        }

        [Test]
        public void Get_LifetimeTerminated_ReturnsAndDeactivatesInstance()
        {
            var pool = new PrefabPool<SpriteRenderer>(_lifetime, _template, _parent);
            var take = _lifetime.Child();

            var instance = pool.Get(take);
            take.Terminate();

            Assert.IsFalse(instance.gameObject.activeSelf);
            Assert.AreEqual(1, pool.FreeCount);
        }

        [Test]
        public void Get_AfterReturn_ReusesInstanceWithoutInstantiate()
        {
            var created = 0;
            var pool = new PrefabPool<SpriteRenderer>(_lifetime, _template, _parent, _ => created++);
            var first = _lifetime.Child();

            var instance = pool.Get(first);
            first.Terminate();
            var again = pool.Get(_lifetime.Child());

            Assert.AreSame(instance, again);
            Assert.IsTrue(again.gameObject.activeSelf);
            Assert.AreEqual(1, created);
            Assert.AreEqual(1, pool.Count);
            Assert.AreEqual(1, _parent.childCount);
        }

        [Test]
        public void Get_NothingFree_GrowsThePool()
        {
            var pool = new PrefabPool<SpriteRenderer>(_lifetime, _template, _parent);

            var first = pool.Get(_lifetime.Child());
            var second = pool.Get(_lifetime.Child());

            Assert.AreNotSame(first, second);
            Assert.AreEqual(2, pool.Count);
            Assert.AreEqual("Template_2", second.name);
        }

        [Test]
        public void Created_RunsOncePerInstanceBeforeGetReturns()
        {
            SpriteRenderer seen = null;
            var pool = new PrefabPool<SpriteRenderer>(_lifetime, _template, _parent, instance => seen = instance);

            var taken = pool.Get(_lifetime.Child());

            Assert.AreSame(taken, seen);
        }

        [Test]
        public void Get_TerminatedLifetime_Throws()
        {
            var pool = new PrefabPool<SpriteRenderer>(_lifetime, _template, _parent);
            var terminated = new Lifetime();
            terminated.Terminate();

            Assert.Throws<InvalidOperationException>(() => pool.Get(terminated));
            Assert.AreEqual(0, pool.Count);
        }

        [Test]
        public void PoolLifetimeTerminated_DestroysTakenAndFreeInstances()
        {
            var lifetime = new Lifetime();
            var pool = new PrefabPool<SpriteRenderer>(lifetime, _template, _parent);
            var freed = lifetime.Child();
            pool.Get(freed);
            freed.Terminate();
            var taken = new Lifetime();
            var instance = pool.Get(taken);

            lifetime.Terminate();
            taken.Terminate();

            Assert.AreEqual(0, _parent.childCount);
            Assert.IsTrue(instance == null);
            Assert.AreEqual(0, pool.Count);
            Assert.IsTrue(_template != null);
            Assert.Throws<InvalidOperationException>(() => pool.Get(new Lifetime()));
        }

        [Test]
        public void Constructor_NoTemplate_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PrefabPool<SpriteRenderer>(_lifetime, null, _parent));
        }
    }
}
