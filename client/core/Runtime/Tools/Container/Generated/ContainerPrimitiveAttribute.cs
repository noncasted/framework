using System;

namespace Internal
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class ContainerPrimitiveAttribute : Attribute
    {
        public ContainerPrimitiveAttribute(ContainerPrimitive kind)
        {
            Kind = kind;
        }

        public ContainerPrimitive Kind { get; }
    }
}
