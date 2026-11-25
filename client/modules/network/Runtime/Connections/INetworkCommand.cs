using System;
using Shared;

namespace Internal
{
    public interface INetworkCommand
    {
        Type ContextType { get; }

        INetworkContext Execute(IReadOnlyLifetime lifetime, INetworkContext context);
    }
}