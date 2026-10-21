using System;

namespace Internal
{
    public interface IContainerRegistry
    {
        ServiceRegistration Add(Type implementation, ServiceLifetime lifetime);
        ServiceRegistration AddInstance(Type serviceType, object instance);
        ServiceRegistration AddComponent(Type serviceType, UnityEngine.Object component, ServiceLifetime lifetime);
        void AddInjection(object target);
    }
}