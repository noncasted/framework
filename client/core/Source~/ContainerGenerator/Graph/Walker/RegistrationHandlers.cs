using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class RegistrationHandlers {
        private readonly LifetimeReader _lifetimeReader;
        private readonly IGraphWalk _walk;

        public RegistrationHandlers(LifetimeReader lifetimeReader, IGraphWalk walk) {
            _lifetimeReader = lifetimeReader;
            _walk = walk;
        }

        public void HandleRegister(WalkState state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            _walk.WalkReceiver(state, invocation);
            var implementation = "";
            var implementationMap = "";
            var service = "";
            var serviceMap = "";
            if (symbol != null && symbol.TypeArguments.Length == 1) {
                implementation = state.Format(symbol.TypeArguments[0]);
                implementationMap = state.Map(symbol.TypeArguments[0]);
            }
            else if (symbol != null && symbol.TypeArguments.Length >= 2) {
                service = state.Format(symbol.TypeArguments[0]);
                serviceMap = state.Map(symbol.TypeArguments[0]);
                implementation = state.Format(symbol.TypeArguments[1]);
                implementationMap = state.Map(symbol.TypeArguments[1]);
            }

            var registration = RegistrationFactory.NewRegistration(state, invocation, "Register", implementation, "ConstructedType");
            registration.TypeMap = implementationMap;
            registration.Lifetime = _lifetimeReader.ReadLifetime(state, invocation, symbol);
            if (string.IsNullOrEmpty(implementation) == false && string.IsNullOrEmpty(service))
                registration.AddService(implementation, implementationMap);
            if (string.IsNullOrEmpty(service) == false)
                registration.AddService(service, serviceMap);

            state.Result.Registrations.Add(registration);
            state.LastRegistration = registration;
            state.LastCallIndex = -1;
        }

        public void HandleRegisterInstance(WalkState state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            _walk.WalkReceiver(state, invocation);
            var argument = invocation.ArgumentList.Arguments.Count > 0 ? invocation.ArgumentList.Arguments[0].Expression : null;
            var implementation = symbol != null && symbol.TypeArguments.Length > 0
                ? state.Format(symbol.TypeArguments[0])
                : argument != null ? state.Format(state.Model.GetTypeInfo(argument).Type) : "";
            var origin = RegistrationFactory.ClassifyValue(state, argument);
            var registration = RegistrationFactory.NewRegistration(state, invocation, "RegisterInstance", implementation, origin);
            registration.Hole = argument != null ? argument.ToString() : "";
            state.Result.Registrations.Add(registration);
            state.LastRegistration = registration;
            state.LastCallIndex = -1;
        }

        public void HandleRegisterComponent(WalkState state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            _walk.WalkReceiver(state, invocation);
            var argument = invocation.ArgumentList.Arguments.Count > 0 ? invocation.ArgumentList.Arguments[0].Expression : null;
            var implementation = symbol != null && symbol.TypeArguments.Length > 0
                ? state.Format(symbol.TypeArguments[0])
                : argument != null ? state.Format(state.Model.GetTypeInfo(argument).Type) : "";
            var origin = RegistrationFactory.ClassifyValue(state, argument);
            if (argument is ThisExpressionSyntax)
                origin = "InjectExisting";
            if (origin == "InstanceHole" && argument != null && RegistrationFactory.LooksLikePrefabAsset(argument))
                origin = "PrefabAsset";
            var registration = RegistrationFactory.NewRegistration(state, invocation, "RegisterComponent", implementation, origin);
            registration.Lifetime = _lifetimeReader.ReadLifetime(state, invocation, symbol);
            registration.Hole = argument != null ? argument.ToString() : "";
            if (string.IsNullOrEmpty(implementation) == false)
                registration.AddService(implementation);
            state.Result.Registrations.Add(registration);
            state.LastRegistration = registration;
            state.LastCallIndex = -1;
        }

        public void HandleAs(WalkState state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            _walk.WalkReceiver(state, invocation);
            var service = "";
            var map = "";
            if (symbol != null && symbol.TypeArguments.Length > 0) {
                service = state.Format(symbol.TypeArguments[0]);
                map = state.Map(symbol.TypeArguments[0]);
            }
            else if (invocation.ArgumentList.Arguments.Count > 0) {
                var argument = invocation.ArgumentList.Arguments[0].Expression;
                if (argument is TypeOfExpressionSyntax typeOf) {
                    var type = state.Model.GetTypeInfo(typeOf.Type).Type;
                    service = state.Format(type);
                    map = state.Map(type);
                }
            }

            if (string.IsNullOrEmpty(service))
                return;

            var target = state.CurrentRegistration(invocation);
            if (target != null) {
                target.AddService(service, map);
                return;
            }

            if (state.LastCallIndex >= 0)
                state.Result.AddAsToCall(state.LastCallIndex, service);
        }

        public void HandleWithParameter(WalkState state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            _walk.WalkReceiver(state, invocation);
            var argument = invocation.ArgumentList.Arguments.Count > 0 ? invocation.ArgumentList.Arguments[0].Expression : null;
            var hole = argument != null ? argument.ToString() : "";
            var type = symbol != null && symbol.TypeArguments.Length > 0
                ? state.Format(symbol.TypeArguments[0])
                : argument != null ? state.Format(state.Model.GetTypeInfo(argument).Type) : "";
            var target = state.CurrentRegistration(invocation);
            if (target != null) {
                target.AddHole(hole);
                if (string.IsNullOrEmpty(type) == false && target.ParameterTypes.Contains(type) == false)
                    target.ParameterTypes.Add(type);
                return;
            }

            if (state.LastCallIndex >= 0) {
                state.Result.AddHoleToCall(state.LastCallIndex, hole);
                state.Result.AddParameterToCall(state.LastCallIndex, type);
            }
        }

        public void HandleInject(WalkState state, InvocationExpressionSyntax invocation) {
            _walk.WalkReceiver(state, invocation);
            var argument = invocation.ArgumentList.Arguments.Count > 0 ? invocation.ArgumentList.Arguments[0].Expression : null;
            var implementation = argument != null ? state.Format(state.Model.GetTypeInfo(argument).Type) : "";
            var registration = RegistrationFactory.NewRegistration(state, invocation, "Inject", implementation, "InjectExisting");
            registration.Hole = argument != null ? argument.ToString() : "";
            state.Result.Registrations.Add(registration);
            state.LastRegistration = registration;
            state.LastCallIndex = -1;
        }

        // builder.Injectable<T>(): the type does not resolve, the scope only calls its [Inject] for runtime instances.
        public void HandleInjectable(WalkState state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            _walk.WalkReceiver(state, invocation);
            var implementation = "";
            var implementationMap = "";
            if (symbol != null && symbol.TypeArguments.Length == 1) {
                implementation = state.Format(symbol.TypeArguments[0]);
                implementationMap = state.Map(symbol.TypeArguments[0]);
            }

            var registration = RegistrationFactory.NewRegistration(state, invocation, "Injectable", implementation, "Injectable");
            registration.TypeMap = implementationMap;
            state.Result.Registrations.Add(registration);
            state.LastRegistration = null;
            state.LastCallIndex = -1;
        }

        public void HandleInstantiate(WalkState state, InvocationExpressionSyntax invocation) {
            _walk.WalkReceiver(state, invocation);
            foreach (var argument in invocation.ArgumentList.Arguments)
                _walk.WalkExpression(state, argument.Expression, true);
            state.LastInstantiate = invocation;
        }

        public void HandleSceneServices(WalkState state, InvocationExpressionSyntax invocation) {
            _walk.WalkReceiver(state, invocation);
            var argument = invocation.ArgumentList.Arguments.Count > 0
                ? invocation.ArgumentList.Arguments[0].Expression.ToString()
                : "";
            var registration = RegistrationFactory.NewRegistration(state, invocation, "SceneServices", "", "SceneServices");
            registration.Hole = argument;
            state.Result.Registrations.Add(registration);
        }

        public void HandleAsSelf(WalkState state, InvocationExpressionSyntax invocation) {
            HandleCurrent(state, invocation, registration => {
                if (string.IsNullOrEmpty(registration.ImplementationType) == false)
                    registration.AddService(registration.ImplementationType);
            });
        }

        public void HandleWithScopeLifetime(WalkState state, InvocationExpressionSyntax invocation) {
            HandleCurrent(state, invocation, registration => registration.AddHole("builder.Lifetime"));
        }

        private void HandleCurrent(WalkState state, InvocationExpressionSyntax invocation, Action<GraphRegistration> apply) {
            _walk.WalkReceiver(state, invocation);
            var target = state.CurrentRegistration(invocation);
            if (target != null)
                apply(target);
        }
    }
}
