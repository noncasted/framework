using System;
using System.Collections.Generic;
using System.Globalization;

namespace ContainerGenerator {
    internal static class PhaseListeners {
        private const string SetupMarker = "global::Internal.IScopeSetup";

        // The setup calls only what is registered as IScopeSetup: a type that implements it under other services alone
        // would never be set up. One object may come in several registrations (an instance, a component), so a type is
        // fine when any registration of it lists the marker.
        public static void ReportUnregisteredSetup(ScopeGraph graph, TypeIndex types) {
            var marker = types.Find(SetupMarker);
            if (marker == null)
                return;

            var listed = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                if (registration.ServiceTypes.Contains(SetupMarker))
                    listed.Add(registration.ImplementationType);
            }

            var reported = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                var implementation = registration.ImplementationType;
                if (string.IsNullOrEmpty(implementation) || listed.Contains(implementation) || reported.Contains(implementation))
                    continue;

                var symbol = types.Find(implementation);
                if (symbol == null || symbol.Implements(marker) == false)
                    continue;

                reported.Add(implementation);
                graph.Diagnostics.Add(new DiagnosticInfo(
                    GraphDescriptors.UnregisteredSetup,
                    registration.Location,
                    implementation,
                    registration.File ?? "",
                    registration.Line.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }
}
