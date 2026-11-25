using System;
using Shared;

namespace Internal
{
    public abstract class ResponseCommand<TContext> : INetworkCommand where TContext : INetworkContext
    {
        public Type ContextType { get; } = typeof(TContext);

        public INetworkContext Execute(IReadOnlyLifetime lifetime, INetworkContext context)
        {
            return Execute(lifetime, (TContext)context);
        }

        protected abstract INetworkContext Execute(IReadOnlyLifetime lifetime, TContext context);
    }
}
