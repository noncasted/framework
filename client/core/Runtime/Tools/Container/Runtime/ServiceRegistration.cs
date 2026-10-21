using System;

namespace Internal
{
    // Registration handle for the installer chain: owner and index, never allocated on the heap.
    // The generator reads service types, lifetime and construction order from the source; the runtime
    // keeps the implementation type (if/switch branches), the ready instance and WithParameter parameters -
    // ContainerBuilder stores them. The interface is implemented for the generator (GraphWalker.IsRegistrationLike):
    // a call through it boxes, so chain extensions take the struct itself.
    public readonly struct ServiceRegistration : IServiceRegistration
    {
        internal ServiceRegistration(ContainerBuilder owner, int index)
        {
            _owner = owner;
            _index = index;
        }

        private readonly ContainerBuilder _owner;
        private readonly int _index;

        public IBuilder Builder => _owner.Builder;
        public Type ImplementationType => _owner.GetImplementation(_index);

        // The runtime does not store service types: the generator knows them, and a hole is matched by instance type.
        public ServiceRegistration AddServiceType(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            _owner.ThrowIfBuilt();
            return this;
        }

        public ServiceRegistration SetParameter(Type type, object value)
        {
            _owner.SetParameter(_index, type, value);
            return this;
        }

        IServiceRegistration IServiceRegistration.AddServiceType(Type serviceType)
        {
            return AddServiceType(serviceType);
        }

        IServiceRegistration IServiceRegistration.SetParameter(Type type, object value)
        {
            return SetParameter(type, value);
        }
    }
}
