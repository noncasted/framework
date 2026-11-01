using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class LoaderServiceHoles {
        public static void EnsureLoaderServices(ScopeGraph graph, Compilation compilation, TypeIndex types) {
            for (var i = 0; i < LoaderServices.Implementations.Length; i++) {
                if (LoaderServices.Implementations[i] == LoaderServices.EventLoopImplementation) {
                    EnsureEventLoopHole(graph, compilation, types);
                    continue;
                }

                var implementation = LoaderServices.Find(compilation, LoaderServices.Implementations[i]);
                if (implementation == null)
                    continue;

                var implementationName = types.Format(implementation);
                if (string.IsNullOrEmpty(implementationName) || HasService(graph, implementationName))
                    continue;

                var registration = new GraphRegistration {
                    Kind = "Register",
                    ImplementationType = implementationName,
                    Lifetime = "Scoped",
                    Origin = LoaderServices.Origin,
                    Source = "Loader",
                };
                var serviceNames = LoaderServices.Services[i];
                for (var s = 0; s < serviceNames.Length; s++) {
                    var service = LoaderServices.Find(compilation, serviceNames[s]);
                    var formatted = types.Format(service);
                    if (string.IsNullOrEmpty(formatted) == false)
                        registration.ServiceTypes.Add(formatted);
                }

                if (HasAnyService(graph, registration))
                    continue;
                graph.Registrations.Add(registration);
            }
        }

        // IEventLoop is the very builder.Events of the loader: AddBeforeBuild / AddAfterDispose hang on it
        // and the loader runs the phases through it. An instance of the scope class would be another loop.
        private static void EnsureEventLoopHole(ScopeGraph graph, Compilation compilation, TypeIndex types) {
            var service = types.Format(LoaderServices.Find(compilation, LoaderServices.EventLoopService));
            if (string.IsNullOrEmpty(service) || HasService(graph, service))
                return;

            var registration = new GraphRegistration {
                Kind = "RegisterInstance",
                ImplementationType = service,
                Lifetime = "Singleton",
                Origin = "InstanceHole",
                Source = "Loader",
                Hole = "builder.Events",
            };
            registration.ServiceTypes.Add(service);
            graph.Registrations.Add(registration);
        }

        private static bool HasService(ScopeGraph graph, string type) {
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                if (registration.ImplementationType == type)
                    return true;
                for (var s = 0; s < registration.ServiceTypes.Count; s++) {
                    if (registration.ServiceTypes[s] == type)
                        return true;
                }
            }

            return false;
        }

        private static bool HasAnyService(ScopeGraph graph, GraphRegistration candidate) {
            if (HasService(graph, candidate.ImplementationType))
                return true;
            for (var i = 0; i < candidate.ServiceTypes.Count; i++) {
                if (HasService(graph, candidate.ServiceTypes[i]))
                    return true;
            }

            return false;
        }
    }
}
