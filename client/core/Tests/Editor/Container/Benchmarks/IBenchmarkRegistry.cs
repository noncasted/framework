using System;

namespace Internal.Tests
{
    internal interface IBenchmarkRegistry
    {
        void Add(Type implementation, ServiceLifetime lifetime, Type[] markers);
    }
}
