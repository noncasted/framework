using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public static class GeneratedScopes
    {
        private static readonly Dictionary<string, Factory> _factories =
            new Dictionary<string, Factory>(StringComparer.Ordinal);

        // Roots split into entity variants by view type: such a root has no class of its own.
        private static readonly HashSet<string> _variantRoots = new HashSet<string>(StringComparer.Ordinal);

        // The entity loader asks for the id on every card, and it does not change for a method and view type:
        // the strings are built once (it used to be ~470 + ~300 bytes per load). The root key is the method handle:
        // the MethodInfo object can differ between delegates of the same method.
        private static readonly Dictionary<IntPtr, string> _rootIds = new Dictionary<IntPtr, string>();

        private static readonly Dictionary<(string, Type), string> _variantKeys =
            new Dictionary<(string, Type), string>();

        // constructed - the implementation types of every Register the class has a slot for; instances - the
        // declared type of every RegisterInstance / RegisterComponent slot (repeated per slot). The builder of
        // the scope is checked against them before the class is built.
        public static void Register(
            string rootId,
            Func<GeneratedScopeRequest, IContainer> factory,
            Type[] constructed,
            Type[] instances)
        {
            if (string.IsNullOrEmpty(rootId) == true)
                throw new ArgumentException("Root id is required.", nameof(rootId));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            if (constructed == null)
                throw new ArgumentNullException(nameof(constructed));

            if (instances == null)
                throw new ArgumentNullException(nameof(instances));

            if (_factories.TryGetValue(rootId, out var existing) == true)
            {
                // Without domain reload, statics survive entering Play mode and RegisterGenerated
                // is called again: the same factory method is not a duplicate but a repeated registration.
                if (existing.Create.Method == factory.Method)
                    return;

                throw new InvalidOperationException(
                    "Generated scope '" + rootId + "' is already registered.");
            }

            _factories.Add(rootId, new Factory(factory, constructed, instances));
        }

        public static bool IsRegistered(string rootId)
        {
            if (string.IsNullOrEmpty(rootId) == true)
                return false;

            return _factories.ContainsKey(rootId);
        }

        public static bool Unregister(string rootId)
        {
            if (string.IsNullOrEmpty(rootId) == true)
                return false;

            return _factories.Remove(rootId);
        }

        public static void RegisterVariant(
            string rootId,
            Type viewType,
            Func<GeneratedScopeRequest, IContainer> factory,
            Type[] constructed,
            Type[] instances)
        {
            if (viewType == null)
                throw new ArgumentNullException(nameof(viewType));

            Register(VariantKey(rootId, viewType), factory, constructed, instances);
            _variantRoots.Add(rootId);
        }

        // The root is built per view type: a view type without its variant class is an error, not the base root.
        public static bool HasVariants(string rootId)
        {
            return _variantRoots.Contains(rootId);
        }

        public static string VariantKey(string rootId, Type viewType)
        {
            if (_variantKeys.TryGetValue((rootId, viewType), out var key) == false)
            {
                key = rootId + "@" + TypeName(viewType);
                _variantKeys.Add((rootId, viewType), key);
            }

            return key;
        }

        /// <summary>
        /// Root id in the form the generator writes it: the type with dots and the method,
        /// for a local function - "+Name" after the outer method. A lambda cannot be a root:
        /// the scope class cannot be found by it.
        /// </summary>
        public static string RootId(MethodInfo method)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            var handle = method.MethodHandle.Value;

            if (_rootIds.TryGetValue(handle, out var id) == false)
            {
                id = BuildRootId(method);
                _rootIds.Add(handle, id);
            }

            return id;
        }

        private static string BuildRootId(MethodInfo method)
        {
            var type = method.DeclaringType;
            var name = method.Name;

            if (name.Length == 0 || name[0] != '<')
                return TypeName(type) + "." + name;

            var close = name.IndexOf('>');
            var marker = close < 0 ? -1 : name.IndexOf("g__", close, StringComparison.Ordinal);
            var bar = marker < 0 ? -1 : name.IndexOf('|', marker);

            if (close < 0 || marker != close + 1 || bar < 0)
            {
                throw new ArgumentException(
                    "Scope root must be a method group or a local function, not a lambda: " + name,
                    nameof(method));
            }

            var outer = name.Substring(1, close - 1);
            var local = name.Substring(marker + 3, bar - marker - 3);

            while (type != null && type.Name.Length > 0 && type.Name[0] == '<')
                type = type.DeclaringType;

            return TypeName(type) + "." + outer + "+" + local;
        }

        private static string TypeName(Type type)
        {
            if (type == null)
                return string.Empty;

            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        internal static IContainer Create(string rootId, ContainerBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (_factories.TryGetValue(rootId, out var factory) == false)
            {
                throw new InvalidOperationException(
                    $"No generated container for '{rootId}'. Check generator diagnostics (CINGR00*) for this root.");
            }

            builder.VerifyCovered(rootId, factory.Constructed, factory.Instances);

            var lifetime = builder.CreateLifetime();
            IContainer created = null;

            try
            {
                var request = new GeneratedScopeRequest(rootId, builder, builder.Parent, lifetime);
                var allocatedBefore = ContainerDiagnostics.AllocatedBytes();
                created = factory.Create.Invoke(request);
                var buildBytes = ContainerDiagnostics.AllocatedBytes() - allocatedBefore;

                if (created == null)
                {
                    throw new InvalidOperationException(
                        $"Generated factory for '{rootId}' returned null.");
                }

                builder.MarkBuilt();
                AttachDebug(created, builder, buildBytes);
                return created;
            }
            catch
            {
                if (created != null)
                    created.Dispose();
                else
                    lifetime.Terminate();

                throw;
            }
        }

        internal static void Clear()
        {
            _factories.Clear();
            _variantRoots.Clear();
        }

        // Rethrows what a generated Dispose collected: one error as it is, several together.
        public static void ThrowDisposeErrors(List<Exception> errors)
        {
            if (errors == null)
                return;

            if (errors.Count == 1)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();

            throw new AggregateException("Several services of the scope failed to dispose.", errors);
        }

        // An alternative (a registration under if / switch) whose branch did not run leaves its field null.
        public static T RequireAlternative<T>(T value, string type) where T : class
        {
            if (value == null)
                throw new InvalidOperationException("No alternative of " + type + " was registered.");

            return value;
        }

        // A list without the alternatives whose branch did not run.
        public static T[] WithoutAbsent<T>(T[] values) where T : class
        {
            var count = 0;

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                    count++;
            }

            if (count == values.Length)
                return values;

            var result = new T[count];
            var next = 0;

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                    result[next++] = values[i];
            }

            return result;
        }

        private readonly struct Factory
        {
            public Factory(Func<GeneratedScopeRequest, IContainer> create, Type[] constructed, Type[] instances)
            {
                Create = create;
                Constructed = constructed;
                Instances = instances;
            }

            public readonly Func<GeneratedScopeRequest, IContainer> Create;
            public readonly Type[] Constructed;
            public readonly Type[] Instances;
        }

        private static void AttachDebug(IContainer created, ContainerBuilder builder, long buildBytes)
        {
#if UNITY_EDITOR || DEBUG
            // LoadAssetGroup records by the scope builder's lifetime, not by the container's child lifetime.
            // Take them even without diagnostics, otherwise records would pile up while it is off.
            var loadedAssets = ContainerRegistryDebug.TakeLoadedAssets(builder.ScopeLifetime);
            var diagnostics = created.Diagnostics;

            if (diagnostics == null)
                return;

            if (diagnostics is ContainerDiagnostics own)
            {
                own.SetLoadedAssets(loadedAssets);
                own.RecordAllocation(null, null, buildBytes, false);
            }

            ContainerRegistryDebug.Add(diagnostics);

            if (created.Lifetime != null)
                created.Lifetime.Listen(() => ContainerRegistryDebug.Remove(diagnostics));
#endif
        }
    }

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