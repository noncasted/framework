using System;

namespace Internal
{
    // InstanceType == null - a constructed registration; otherwise RegisterInstance / RegisterComponent with
    // its declared type and the ready instance.
    internal readonly struct ContainerEntry
    {
        public ContainerEntry(Type implementation, Type instanceType, object instance)
        {
            Implementation = implementation;
            InstanceType = instanceType;
            Instance = instance;
        }

        public readonly Type Implementation;
        public readonly Type InstanceType;
        public readonly object Instance;
    }
}
