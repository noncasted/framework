using System;

namespace Internal
{
    // Chains in installers are written with BuilderExtensions extensions (As, AsSelf, WithParameter, ...):
    // the generator builds the graph from them. Methods here have different names so they do not intercept those calls.
    public interface IServiceRegistration
    {
        // The builder the registration is written to: chain extensions register siblings through it.
        IBuilder Builder { get; }
        Type ImplementationType { get; }
        IServiceRegistration AddServiceType(Type serviceType);
        IServiceRegistration SetParameter(Type type, object value);
    }
}