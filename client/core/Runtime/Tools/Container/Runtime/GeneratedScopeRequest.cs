using System;

namespace Internal
{
    public readonly struct GeneratedScopeRequest
    {
        internal GeneratedScopeRequest(
            string rootId,
            ContainerBuilder builder,
            IContainer parent,
            ILifetime lifetime)
        {
            RootId = rootId ?? string.Empty;
            _builder = builder;
            Parent = parent;
            Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            Name = builder != null ? builder.Name : rootId;
        }

        private readonly ContainerBuilder _builder;

        public string RootId { get; }
        public string Name { get; }
        public IContainer Parent { get; }
        public ILifetime Lifetime { get; }

        public T Get<T>()
        {
            if (TryGet(typeof(T), out var instance) == false)
            {
                throw new InvalidOperationException(
                    $"No hole or instance for type {typeof(T).FullName}.");
            }

            return (T)instance;
        }

        // The instance of the occurrence-th RegisterInstance / RegisterComponent of T: one slot per such call.
        public T GetInstance<T>(int occurrence)
        {
            if (_builder == null)
                throw new InvalidOperationException($"No builder to take instance {occurrence} of {typeof(T).FullName}.");

            return (T)_builder.GetInstance(typeof(T), occurrence);
        }

        // A parameter of the occurrence-th registration of the implementation: its own WithParameter value, otherwise
        // the parent scope (the generator lets a registration with WithParameter take what it cannot bind from both).
        public T GetParameter<T>(Type implementation, int occurrence)
        {
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));

            if (_builder == null)
                throw new InvalidOperationException($"No builder to take parameter {typeof(T).FullName} of {implementation.FullName}.");

            if (_builder.TryGetParameter(implementation, occurrence, typeof(T), out var value) == true)
                return (T)value;

            if (Parent != null && Parent.TryResolve(typeof(T), out var resolved) == true)
                return (T)resolved;

            throw new InvalidOperationException(
                $"No parameter {typeof(T).FullName} for {implementation.FullName}: neither WithParameter nor the parent scope gives it.");
        }

        // An export of a parent scope: the generator found it there and not in this scope.
        public T GetFromParent<T>()
        {
            if (Parent == null)
                throw new InvalidOperationException($"No parent scope to take {typeof(T).FullName} from.");

            return (T)Parent.Resolve(typeof(T));
        }

        // builder.Inject instances: [Inject] is called on each of them.
        public T[] GetInjections<T>()
        {
            if (_builder == null)
                throw new InvalidOperationException($"No builder to take injections of {typeof(T).FullName}.");

            var injections = _builder.GetInjections<T>();

            if (injections.Length == 0)
                throw new InvalidOperationException($"No injections of type {typeof(T).FullName}.");

            return injections;
        }

        // Which of the installer alternatives (if/switch branch) actually got into this scope.
        public bool IsRegistered(Type implementation)
        {
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));

            return _builder != null && _builder.HasRegistration(implementation);
        }

        public bool TryGet(Type type, out object instance)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (_builder != null && _builder.TryGetHole(type, out instance) == true)
                return true;

            if (Parent != null && Parent.TryResolve(type, out instance) == true)
                return true;

            instance = null;
            return false;
        }
    }
}
