using System;

namespace Internal
{
    internal readonly struct GeneratedScopeFactory
    {
        public GeneratedScopeFactory(Func<GeneratedScopeRequest, IContainer> create, Type[] constructed, Type[] instances)
        {
            Create = create;
            Constructed = constructed;
            Instances = instances;
        }

        public readonly Func<GeneratedScopeRequest, IContainer> Create;
        public readonly Type[] Constructed;
        public readonly Type[] Instances;
    }
}
