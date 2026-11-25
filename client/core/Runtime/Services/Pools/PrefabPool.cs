using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // Instances of a template prefab under one parent, without an Instantiate per use. Get takes a free instance
    // (or creates one) for a lifetime and gives it back to the pool when that lifetime ends: a cancelled caller
    // never leaves its instance in the scene. A free instance is inactive, a taken one active; everything else
    // (position, colours, animation) is left as the last user made it, so the user resets it on every take.
    // The lifetime of the pool destroys every instance, taken or free.
    public sealed class PrefabPool<T> where T : Component
    {
        public PrefabPool(IReadOnlyLifetime lifetime, T template, Transform parent)
            : this(lifetime, template, parent, static _ => { })
        {
        }

        // created runs once per instance, right after its Instantiate: injection (IViewInjector.Inject), layers.
        public PrefabPool(IReadOnlyLifetime lifetime, T template, Transform parent, Action<T> created)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template), $"[PrefabPool<{typeof(T).Name}>] No template");

            if (parent == null)
                throw new ArgumentNullException(nameof(parent), $"[PrefabPool<{typeof(T).Name}>] No parent");

            _template = template;
            _parent = parent;
            _created = created ?? throw new ArgumentNullException(nameof(created));
            _lifetime = lifetime;

            lifetime.Listen(DestroyAll);
        }

        private readonly T _template;
        private readonly Transform _parent;
        private readonly Action<T> _created;
        private readonly IReadOnlyLifetime _lifetime;
        private readonly List<Slot> _slots = new();
        private readonly Stack<Slot> _free = new();

        // Every instance made by the pool, taken or free.
        public int Count => _slots.Count;
        public int FreeCount => _free.Count;

        public T Get(IReadOnlyLifetime lifetime)
        {
            if (_lifetime.IsTerminated == true)
                throw new InvalidOperationException($"[PrefabPool<{typeof(T).Name}>] The pool of {_template.name} is destroyed");

            // A terminated lifetime would give the instance back at once while the caller still uses it.
            if (lifetime.IsTerminated == true)
                throw new InvalidOperationException($"[PrefabPool<{typeof(T).Name}>] Get of {_template.name} with a terminated lifetime");

            Slot slot;

            if (_free.Count > 0)
            {
                slot = _free.Pop();
                slot.Instance.gameObject.SetActive(true);
            }
            else
            {
                slot = Create();
            }

            slot.Take();
            lifetime.Listen(slot.Return);

            return slot.Instance;
        }

        private Slot Create()
        {
            var instance = Object.Instantiate(_template, _parent, false);
            instance.name = $"{_template.name}_{_slots.Count + 1}";

            var slot = new Slot(this, instance);
            _slots.Add(slot);
            _created(instance);

            return slot;
        }

        private void Return(Slot slot)
        {
            // The pool is gone with its instances: nothing to give back.
            if (_lifetime.IsTerminated == true)
                return;

            // A scene unload destroys the parent with the instances before the lifetimes end: it is not reused.
            if (slot.Instance == null)
                return;

            slot.Instance.gameObject.SetActive(false);
            _free.Push(slot);
        }

        private void DestroyAll()
        {
            foreach (var slot in _slots)
            {
                // On a scene unload or an application quit Unity destroys the objects before the lifetimes terminate.
                if (slot.Instance == null)
                    continue;

                // Edit mode (tests, editor previews) cannot destroy deferred.
                if (Application.isPlaying == true)
                    Object.Destroy(slot.Instance.gameObject);
                else
                    Object.DestroyImmediate(slot.Instance.gameObject);
            }

            _slots.Clear();
            _free.Clear();
        }

        // One instance with its cached return callback: a take allocates no delegate.
        private sealed class Slot
        {
            public Slot(PrefabPool<T> pool, T instance)
            {
                _pool = pool;
                Instance = instance;
                Return = OnReturn;
            }

            private readonly PrefabPool<T> _pool;

            private bool _isTaken;

            public T Instance { get; }
            public Action Return { get; }

            public void Take()
            {
                _isTaken = true;
            }

            private void OnReturn()
            {
                if (_isTaken == false)
                    throw new InvalidOperationException($"[PrefabPool<{typeof(T).Name}>] {Instance.name} is returned twice");

                _isTaken = false;
                _pool.Return(this);
            }
        }
    }
}
