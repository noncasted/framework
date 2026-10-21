using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    // Collects registrations and scope holes. Only the generated class (GeneratedScopes) builds the container.
    [NoAutoStaticsCleanup]
    public sealed class ContainerBuilder : IContainerRegistry
    {
        public ContainerBuilder(string name = "Root", IReadOnlyLifetime hostLifetime = null)
        {
            Name = name;
            _hostLifetime = hostLifetime;
        }

        // scopeLifetime is the loader's scope lifetime: the container lives as its child.
        public ContainerBuilder(string name, IContainer parent, IReadOnlyLifetime scopeLifetime)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            Name = name;
            Parent = parent;
            _hostLifetime = parent.Lifetime;
            _scopeLifetime = scopeLifetime;
        }

        private readonly IReadOnlyLifetime _hostLifetime;
        private readonly IReadOnlyLifetime _scopeLifetime;

        // The lists live from the first registration until MarkBuilt and go back to the pool: building runs only
        // on the main thread (ContainerThread.Assert), and array growth is paid once per process.
        private static readonly Stack<List<Type>> _implementationsPool = new Stack<List<Type>>();
        private static readonly Stack<List<Hole>> _holesPool = new Stack<List<Hole>>();

        // Implementation type of each registration by its index: installer if/switch branches are looked up by it.
        private List<Type> _implementations;

        // Ready instances and WithParameter parameters: what the generated class receives as holes.
        private List<Hole> _holes;
        private List<object> _injections;

        private bool _built;

        public string Name { get; }

        internal IContainer Parent { get; }

        internal bool IsBuilt => _built;

        // The Lifetime the installer sees through IBuilder.Lifetime.
        internal IReadOnlyLifetime ScopeLifetime => _scopeLifetime ?? _hostLifetime;

        // The IBuilder that writes into this ContainerBuilder. Registrations hand it to chain extensions
        // (WithScopeLifetime).
        internal IBuilder Builder { get; private set; }

        // The generator reads lifetime from the call; the generated class already knows it, the runtime does not store it.
        public ServiceRegistration Add(Type implementation, ServiceLifetime lifetime)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();

            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));

            return new ServiceRegistration(this, AddImplementation(implementation));
        }

        public ServiceRegistration AddInstance(Type serviceType, object instance)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var index = AddImplementation(instance.GetType());
            AddHole(new Hole(index, null, instance));
            return new ServiceRegistration(this, index);
        }

        public ServiceRegistration AddComponent(
            Type serviceType,
            UnityEngine.Object component,
            ServiceLifetime lifetime)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (component == null)
                throw new ArgumentNullException(nameof(component));

            var index = AddImplementation(component.GetType());
            AddHole(new Hole(index, null, component));
            return new ServiceRegistration(this, index);
        }

        // Calls the builder constructor. One ContainerBuilder - one IBuilder.
        internal void AttachBuilder(IBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (Builder != null && Builder != builder)
                throw new InvalidOperationException(
                    $"Container builder '{Name}' is already attached to another builder.");

            Builder = builder;
        }

        public void AddInjection(object target)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();

            if (target == null)
                throw new ArgumentNullException(nameof(target));

            _injections ??= new List<object>();
            _injections.Add(target);
        }

        internal ILifetime CreateLifetime()
        {
            if (_scopeLifetime != null)
                return _scopeLifetime.Child();

            if (Parent != null)
                return Parent.Lifetime.Child();

            if (_hostLifetime != null)
                return _hostLifetime.Child();

            return new Lifetime();
        }

        internal void MarkBuilt()
        {
            _built = true;

            if (_implementations != null)
            {
                _implementations.Clear();
                _implementationsPool.Push(_implementations);
                _implementations = null;
            }

            if (_holes != null)
            {
                _holes.Clear();
                _holesPool.Push(_holes);
                _holes = null;
            }
        }

        internal Type GetImplementation(int index)
        {
            ThrowIfBuilt();
            return _implementations[index];
        }

        internal void SetParameter(int index, Type type, object value)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();

            if (type == null)
                throw new ArgumentNullException(nameof(type));

            // A repeated WithParameter of the same type on the same registration replaces the value.
            for (var i = 0; i < (_holes?.Count ?? 0); i++)
            {
                if (_holes[i].Index == index && _holes[i].Type == type)
                {
                    _holes[i] = new Hole(index, type, value);
                    return;
                }
            }

            AddHole(new Hole(index, type, value));
        }

        internal bool HasRegistration(Type implementation)
        {
            ThrowIfBuilt();

            for (var i = 0; i < (_implementations?.Count ?? 0); i++)
            {
                if (_implementations[i] == implementation)
                    return true;
            }

            return false;
        }

        internal bool TryGetHole(Type type, out object instance)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            ThrowIfBuilt();

            // The last registration wins; within it, the ready instance wins, then the parameter. A parameter
            // can be appended to an earlier registration later (a chain extension), so
            // the write order does not match the registration order and the whole list is scanned.
            var found = -1;

            for (var i = (_holes?.Count ?? 0) - 1; i >= 0; i--)
            {
                var hole = _holes[i];

                var matches = hole.Type == null
                    ? type.IsInstanceOfType(hole.Value) == true
                    : hole.Type == type;

                if (matches == false)
                    continue;

                if (found < 0 || hole.Index > _holes[found].Index ||
                    (hole.Index == _holes[found].Index && hole.Type == null))
                    found = i;
            }

            if (found >= 0)
            {
                instance = _holes[found].Value;
                return true;
            }

            // builder.Inject(x): the generated class receives the instance as a hole and calls Construct itself.
            for (var i = (_injections?.Count ?? 0) - 1; i >= 0; i--)
            {
                if (type.IsInstanceOfType(_injections[i]) == true)
                {
                    instance = _injections[i];
                    return true;
                }
            }

            instance = null;
            return false;
        }

        // Every instance passed to Inject with this type (identical scene services): [Inject] is called on each.
        internal T[] GetInjections<T>()
        {
            ThrowIfBuilt();

            var count = 0;

            for (var i = 0; i < (_injections?.Count ?? 0); i++)
            {
                if (_injections[i] is T)
                    count++;
            }

            var result = new T[count];
            var next = 0;

            for (var i = 0; i < (_injections?.Count ?? 0); i++)
            {
                if (_injections[i] is T injection)
                    result[next++] = injection;
            }

            return result;
        }

        internal void ThrowIfBuilt()
        {
            if (_built == true)
                throw new InvalidOperationException("Cannot register after the container is created.");
        }

        private int AddImplementation(Type implementation)
        {
            _implementations ??= _implementationsPool.Count > 0 ? _implementationsPool.Pop() : new List<Type>();
            _implementations.Add(implementation);
            return _implementations.Count - 1;
        }

        private void AddHole(Hole hole)
        {
            _holes ??= _holesPool.Count > 0 ? _holesPool.Pop() : new List<Hole>();
            _holes.Add(hole);
        }

        // Type == null - the ready instance of registration Index: matches any type it implements.
        // Otherwise a WithParameter parameter: matches only its own type.
        private readonly struct Hole
        {
            public Hole(int index, Type type, object value)
            {
                Index = index;
                Type = type;
                Value = value;
            }

            public readonly int Index;
            public readonly Type Type;
            public readonly object Value;
        }
    }
}