using System;
using Object = UnityEngine.Object;

namespace Internal
{
    // The container generator recognizes these methods by [ContainerPrimitive]; the kind, not the name, tells it
    // what the call registers.
    public static class BuilderExtensions
    {
        [ContainerPrimitive(ContainerPrimitive.Register)]
        public static ServiceRegistration Register<T>(
            this IBuilder builder,
            ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            return builder.Registry.Add(typeof(T), lifetime).AsSelf();
        }

        [ContainerPrimitive(ContainerPrimitive.RegisterInstance)]
        public static ServiceRegistration RegisterInstance<T>(
            this IBuilder builder,
            T instance)
        {
            if (instance == null)
                throw new NullReferenceException();

            return builder.Registry.AddInstance(typeof(T), instance);
        }

        [ContainerPrimitive(ContainerPrimitive.RegisterComponent)]
        public static ServiceRegistration RegisterComponent<T>(
            this IBuilder builder,
            T component,
            ServiceLifetime lifetime = ServiceLifetime.Singleton) where T : Object
        {
            if (component == null)
                throw new NullReferenceException($"Missing {typeof(T).Name} component");

            return builder.Registry.AddComponent(typeof(T), component, lifetime).AsSelf();
        }

        [ContainerPrimitive(ContainerPrimitive.As)]
        public static ServiceRegistration As<T>(this ServiceRegistration registration)
        {
            return registration.AddServiceType(typeof(T));
        }

        [ContainerPrimitive(ContainerPrimitive.As)]
        public static ServiceRegistration As(this ServiceRegistration registration, Type type)
        {
            return registration.AddServiceType(type);
        }

        [ContainerPrimitive(ContainerPrimitive.WithParameter)]
        public static ServiceRegistration WithParameter<T>(this ServiceRegistration registration, T parameter)
        {
            return registration.SetParameter(typeof(T), parameter);
        }

        [ContainerPrimitive(ContainerPrimitive.AsSelf)]
        public static ServiceRegistration AsSelf(this ServiceRegistration registration)
        {
            return registration.AddServiceType(registration.ImplementationType);
        }

        // Creation without dependents is handled by the generated scope class; there is nothing to mark at runtime.
        [ContainerPrimitive(ContainerPrimitive.AsSelfResolvable)]
        public static ServiceRegistration AsSelfResolvable(this ServiceRegistration registration)
        {
            return registration;
        }

        [ContainerPrimitive(ContainerPrimitive.Inject)]
        public static void Inject<T>(this IBuilder builder, T component)
        {
            if (component == null)
                throw new NullReferenceException("No component provided");

            builder.Registry.AddInjection(component);
        }

        // The scope supports Inject(T) for runtime instances. The Inject branch is written by the generated scope class.
        [ContainerPrimitive(ContainerPrimitive.Injectable)]
        public static void Injectable<T>(this IBuilder builder) where T : class
        {
        }

        [ContainerPrimitive(ContainerPrimitive.WithScopeLifetime)]
        public static ServiceRegistration WithScopeLifetime(this ServiceRegistration registration)
        {
            return registration.SetParameter(typeof(IReadOnlyLifetime), registration.Builder.Lifetime);
        }
    }
}