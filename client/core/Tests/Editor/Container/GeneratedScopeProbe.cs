using System;
using System.Collections.Generic;

namespace Internal.Tests
{
    // A hand-written stand-in of a generated class: drives GeneratedScopes and ContainerBuilder through their public
    // API with the request a real factory gets, without the generator.
    internal sealed class GeneratedScopeProbe : IContainer
    {
        public GeneratedScopeProbe(ILifetime lifetime, IContainerDiagnostics diagnostics = null, params object[] exports)
        {
            _lifetime = lifetime;
            _exports = exports;
            Diagnostics = diagnostics;
        }

        private readonly ILifetime _lifetime;
        private readonly object[] _exports;

        public IContainerDiagnostics Diagnostics { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;
        public bool IsDisposed { get; private set; }

        // Registers the factory under a root of its own, builds it and removes the root.
        public static IContainer Build(
            ContainerBuilder builder,
            Func<GeneratedScopeRequest, IContainer> factory,
            Type[] constructed = null,
            Type[] instances = null)
        {
            var rootId = "Probe." + Guid.NewGuid();
            GeneratedScopes.Register(rootId, factory, constructed ?? Array.Empty<Type>(), instances ?? Array.Empty<Type>());

            try
            {
                return ScopeContainer.Create(rootId, builder);
            }
            finally
            {
                GeneratedScopes.Unregister(rootId);
            }
        }

        // Builds a scope, gives its request to the check and returns the request: it stays usable only inside the check.
        public static GeneratedScopeRequest Inspect(
            ContainerBuilder builder,
            Action<GeneratedScopeRequest> check,
            Type[] constructed = null,
            Type[] instances = null)
        {
            var taken = default(GeneratedScopeRequest);

            Build(builder, request =>
            {
                taken = request;
                check(request);
                return new GeneratedScopeProbe(request.Lifetime);
            }, constructed, instances);

            return taken;
        }

        public object Resolve(Type type)
        {
            if (TryResolve(type, out var instance) == false)
                throw new InvalidOperationException("No export " + type.FullName);

            return instance;
        }

        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        public bool TryResolve(Type type, out object instance)
        {
            for (var i = 0; i < _exports.Length; i++)
            {
                if (type.IsInstanceOfType(_exports[i]) == true)
                {
                    instance = _exports[i];
                    return true;
                }
            }

            instance = null;
            return false;
        }

        public IReadOnlyList<T> ResolveAll<T>()
        {
            throw new NotSupportedException();
        }

        public void Inject(object target)
        {
            throw new NotSupportedException();
        }

        public void InjectGameObject(UnityEngine.GameObject target)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
            IsDisposed = true;
            _lifetime.Terminate();
        }
    }
}
