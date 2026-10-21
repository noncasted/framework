using System;

namespace Internal
{
    // The inject method. The generator finds it only by this attribute, the method name does not matter.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class InjectAttribute : Attribute
    {
    }
}