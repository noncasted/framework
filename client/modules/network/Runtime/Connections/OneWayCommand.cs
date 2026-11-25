using System;
using Shared;

namespace Internal
{
    public abstract class OneWayCommand<TContext> : INetworkCommand where TContext : INetworkContext
    {
        public Type ContextType { get; } = typeof(TContext);

        public INetworkContext Execute(IReadOnlyLifetime lifetime, INetworkContext context)
        {
            Execute(lifetime, (TContext)context);
            return EmptyResponse.Ok;
        }

        protected abstract void Execute(IReadOnlyLifetime lifetime, TContext context);
    }
}
