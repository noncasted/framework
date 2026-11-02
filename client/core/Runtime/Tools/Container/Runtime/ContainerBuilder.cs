using System;

namespace Internal
{
    // Collects registrations and scope holes. Only the generated class (GeneratedScopes) builds the container.
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

        private ScopeRegistrations _registrations;
        private bool _built;

        public string Name { get; }

        internal IContainer Parent { get; }

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

            return AddEntry(new ContainerEntry(implementation, null, null));
        }

        public ServiceRegistration AddInstance(Type serviceType, object instance)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();

            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            return AddEntry(new ContainerEntry(instance.GetType(), serviceType, instance));
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

            return AddEntry(new ContainerEntry(component.GetType(), serviceType, component));
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

            _registrations.AddInjection(target);
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
            _registrations.ReturnToPool();
        }

        internal Type GetImplementation(int index)
        {
            ThrowIfBuilt();
            return _registrations.GetImplementation(index);
        }

        internal void SetParameter(int index, Type type, object value)
        {
            ContainerThread.Assert();
            ThrowIfBuilt();

            if (type == null)
                throw new ArgumentNullException(nameof(type));

            _registrations.SetParameter(index, type, value);
        }

        internal bool HasRegistration(Type implementation)
        {
            ThrowIfBuilt();
            return _registrations.HasRegistration(implementation);
        }

        internal object GetInstance(Type serviceType, int occurrence)
        {
            ThrowIfBuilt();
            return _registrations.GetInstance(Name, serviceType, occurrence);
        }

        internal bool TryGetParameter(Type implementation, int occurrence, Type type, out object value)
        {
            ThrowIfBuilt();
            return _registrations.TryGetParameter(Name, implementation, occurrence, type, out value);
        }

        internal void VerifyCovered(string rootId, Type[] constructed, Type[] instances)
        {
            ThrowIfBuilt();
            _registrations.VerifyCovered(rootId, constructed, instances);
        }

        internal bool TryGetHole(Type type, out object instance)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            ThrowIfBuilt();
            return _registrations.TryGetHole(type, out instance);
        }

        internal T[] GetInjections<T>()
        {
            ThrowIfBuilt();
            return _registrations.GetInjections<T>();
        }

        internal void ThrowIfBuilt()
        {
            if (_built == true)
                throw new InvalidOperationException("Cannot register after the container is created.");
        }

        private ServiceRegistration AddEntry(ContainerEntry entry)
        {
            return new ServiceRegistration(this, _registrations.AddEntry(entry));
        }
    }
}
