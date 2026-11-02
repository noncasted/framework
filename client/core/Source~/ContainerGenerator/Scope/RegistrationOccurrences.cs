namespace ContainerGenerator {
    // How the runtime counts the registrations of one type: the generated class asks the request for the n-th value.
    internal static class RegistrationOccurrences {
        public static bool IsInstanceKind(string kind) {
            return kind == "RegisterInstance" || kind == "RegisterComponent";
        }

        // The runtime counts the instances of a declared type in the order the installer wrote them:
        // the slot takes the one at its place among the instance registrations of that type.
        public static int InstanceOccurrence(ScopeGraph graph, int index) {
            var registration = graph.Registrations[index];
            var occurrence = 0;
            for (var i = 0; i < index; i++) {
                var other = graph.Registrations[i];
                if (IsInstanceKind(other.Kind) && other.ImplementationType == registration.ImplementationType)
                    occurrence++;
            }

            return occurrence;
        }

        // The same for an arm of a switch: its implementation is not the one of the registration.
        public static int ArmOccurrence(ScopeGraph graph, int index, string implementation) {
            var registration = graph.Registrations[index];
            var occurrence = 0;
            for (var i = 0; i < index; i++) {
                var other = graph.Registrations[i];
                if (IsRuntimeRegister(other) && other.ImplementationType == implementation &&
                    BranchGroups.Exclude(other, registration) == false)
                    occurrence++;
            }

            return occurrence;
        }

        // The same for the Register calls of one implementation (the owners of WithParameter values).
        public static int ConstructedOccurrence(ScopeGraph graph, int index) {
            var registration = graph.Registrations[index];
            var occurrence = 0;
            for (var i = 0; i < index; i++) {
                var other = graph.Registrations[i];
                // Another arm of the same statement does not get into the builder together with this one.
                if (IsRuntimeRegister(other) && other.ImplementationType == registration.ImplementationType &&
                    BranchGroups.Exclude(other, registration) == false)
                    occurrence++;
            }

            return occurrence;
        }

        // A Register the installer runs (the loader services are added by the generator, not registered at runtime).
        public static bool IsRuntimeRegister(GraphRegistration registration) {
            return registration.Kind == "Register" && registration.Origin != LoaderServices.Origin &&
                   registration.Origin != "Injectable" && registration.Origin != "ExternalInstaller";
        }
    }
}
