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
        private static readonly Stack<List<Entry>> _entriesPool = new Stack<List<Entry>>();
        private static readonly Stack<List<Hole>> _holesPool = new Stack<List<Hole>>();

        // Every registration by its index, in the order the installer wrote them: the implementation type
        // (installer if/switch branches are looked up by it) and the ready instance with its declared type.
        private List<Entry> _entries;

        // WithParameter parameters by the index of their registration.
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

            return new ServiceRegistration(this, AddEntry(new Entry(implementation, null, null)));
        }

        public ServiceRegistration AddInstance(Type serviceType, object instance)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            return new ServiceRegistration(this, AddEntry(new Entry(instance.GetType(), serviceType, instance)));
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

            return new ServiceRegistration(this, AddEntry(new Entry(component.GetType(), serviceType, component)));
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

            if (_entries != null)
            {
                _entries.Clear();
                _entriesPool.Push(_entries);
                _entries = null;
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
            return _entries[index].Implementation;
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

            for (var i = 0; i < (_entries?.Count ?? 0); i++)
            {
                if (_entries[i].Implementation == implementation)
                    return true;
            }

            return false;
        }

        // The instance of the occurrence-th RegisterInstance / RegisterComponent with this declared type: the
        // generated class has a slot per such call and takes the instances in the order the installer wrote them.
        internal object GetInstance(Type serviceType, int occurrence)
        {
            ThrowIfBuilt();

            var seen = 0;

            for (var i = 0; i < (_entries?.Count ?? 0); i++)
            {
                if (_entries[i].InstanceType != serviceType)
                    continue;

                if (seen == occurrence)
                    return _entries[i].Instance;

                seen++;
            }

            throw new InvalidOperationException(
                $"Scope '{Name}' has {seen} instance(s) of {serviceType.FullName}, the generated class expects at least " +
                $"{occurrence + 1}: a registration of it did not run (an if without else around RegisterInstance).");
        }

        // The WithParameter value for a parameter of this type on the occurrence-th registration of the implementation.
        internal bool TryGetParameter(Type implementation, int occurrence, Type type, out object value)
        {
            ThrowIfBuilt();

            var seen = 0;

            for (var i = 0; i < (_entries?.Count ?? 0); i++)
            {
                if (_entries[i].InstanceType != null || _entries[i].Implementation != implementation)
                    continue;

                if (seen++ != occurrence)
                    continue;

                for (var h = 0; h < (_holes?.Count ?? 0); h++)
                {
                    if (_holes[h].Index == i && _holes[h].Type == type)
                    {
                        value = _holes[h].Value;
                        return true;
                    }
                }

                // A value the parameter can hold: the generator has checked that there is one such (CINGR014).
                for (var h = 0; h < (_holes?.Count ?? 0); h++)
                {
                    if (_holes[h].Index == i && type.IsAssignableFrom(_holes[h].Type) == true)
                    {
                        value = _holes[h].Value;
                        return true;
                    }
                }

                value = null;
                return false;
            }

            throw new InvalidOperationException(
                $"Scope '{Name}' has {seen} registration(s) of {implementation.FullName}, the generated class expects " +
                $"at least {occurrence + 1}.");
        }

        // Every runtime registration must have a slot in the generated class: one the generator did not see
        // (a call through an interface or a virtual method, a stale asset map) would otherwise be dropped silently,
        // and the instances of a loop would all go into one slot.
        internal void VerifyCovered(string rootId, Type[] constructed, Type[] instances)
        {
            ThrowIfBuilt();

            for (var i = 0; i < (_entries?.Count ?? 0); i++)
            {
                var entry = _entries[i];

                if (entry.InstanceType == null)
                {
                    if (Array.IndexOf(constructed, entry.Implementation) < 0)
                    {
                        throw new InvalidOperationException(
                            $"Scope '{rootId}': {entry.Implementation.FullName} is registered at runtime, but the " +
                            "generated class does not know it. It was registered through code the generator does not " +
                            "follow (a call through an interface or a virtual method, reflection), or the asset map " +
                            "of the project is stale.");
                    }

                    continue;
                }

                var registered = 0;

                for (var j = 0; j < _entries.Count; j++)
                {
                    if (_entries[j].InstanceType == entry.InstanceType)
                        registered++;
                }

                var slots = 0;

                for (var j = 0; j < instances.Length; j++)
                {
                    if (instances[j] == entry.InstanceType)
                        slots++;
                }

                if (registered > slots)
                {
                    throw new InvalidOperationException(
                        $"Scope '{rootId}': {entry.InstanceType.FullName} is registered as an instance {registered} " +
                        $"time(s), the generated class has {slots} slot(s) for it. A registration in a loop, a repeated " +
                        "installer call, identical scene services or code the generator does not follow: one slot " +
                        "keeps one instance (builder.Inject takes every instance of a type).");
                }
            }
        }

        internal bool TryGetHole(Type type, out object instance)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            ThrowIfBuilt();

            // By type only, the last registration wins; within it, the ready instance wins, then the parameter.
            // What a generated slot owns is taken by its registration (GetInstance, TryGetParameter): this lookup is
            // left to the switch arm parameters and the builder.Inject slots.
            for (var i = (_entries?.Count ?? 0) - 1; i >= 0; i--)
            {
                if (_entries[i].InstanceType != null && type.IsInstanceOfType(_entries[i].Instance) == true)
                {
                    instance = _entries[i].Instance;
                    return true;
                }

                for (var h = (_holes?.Count ?? 0) - 1; h >= 0; h--)
                {
                    if (_holes[h].Index == i && _holes[h].Type == type)
                    {
                        instance = _holes[h].Value;
                        return true;
                    }
                }
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

        private int AddEntry(Entry entry)
        {
            _entries ??= _entriesPool.Count > 0 ? _entriesPool.Pop() : new List<Entry>();
            _entries.Add(entry);
            return _entries.Count - 1;
        }

        private void AddHole(Hole hole)
        {
            _holes ??= _holesPool.Count > 0 ? _holesPool.Pop() : new List<Hole>();
            _holes.Add(hole);
        }

        // InstanceType == null - a constructed registration; otherwise RegisterInstance / RegisterComponent with
        // its declared type and the ready instance.
        private readonly struct Entry
        {
            public Entry(Type implementation, Type instanceType, object instance)
            {
                Implementation = implementation;
                InstanceType = instanceType;
                Instance = instance;
            }

            public readonly Type Implementation;
            public readonly Type InstanceType;
            public readonly object Instance;
        }

        // A WithParameter parameter of registration Index: matches only its own type.
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