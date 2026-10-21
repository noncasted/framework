using System;
using System.Collections.Generic;

namespace Internal
{
    public sealed class ObjectStateDefinition : IStateDefinition
    {
        public ObjectStateDefinition(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        public Guid Id { get; }
        public string Name { get; }

        public bool IsTransitable(IStateDefinition definition)
        {
            return definition.Id != Id;
        }
    }
}
