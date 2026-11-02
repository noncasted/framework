using System;

namespace Internal
{
    // A WithParameter parameter of registration Index: matches only its own type.
    internal readonly struct ContainerHole
    {
        public ContainerHole(int index, Type type, object value)
        {
            Index = index;
            Type = type;
            Value = value;
        }

        public readonly int Index;
        public readonly Type Type;
        public readonly object Value;
    }
}
