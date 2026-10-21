using System;

namespace Internal
{
    public interface IStateDefinition
    {
        Guid Id { get; }
        string Name { get; }

        bool IsTransitable(IStateDefinition definition);
    }
}