using System;
using System.Collections.Generic;
using System.Globalization;

namespace ContainerGenerator {
    internal static class PhaseListeners {
        private static readonly string[] PhaseMarkers = {
            "global::Internal.IScopeBaseSetup",
            "global::Internal.IScopeBaseSetupAsync",
            "global::Internal.IScopeSetup",
            "global::Internal.IScopeSetupAsync",
            "global::Internal.IScopeSetupCompletion",
            "global::Internal.IScopeSetupCompletionAsync",
            "global::Internal.IScopeLoaded",
            "global::Internal.IScopeLoadedAsync",
            "global::Internal.IScopeDispose",
            "global::Internal.IScopeDisposeAsync",
        };

        // Every phase resolves its listeners anew: a transient that listens to setup and dispose would get them
        // on two different instances.
        public static void ReportTransientPhaseListeners(ScopeGraph graph) {
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                if (registration.Lifetime != "Transient")
                    continue;

                var phases = new List<string>();
                for (var s = 0; s < registration.ServiceTypes.Count; s++) {
                    if (Array.IndexOf(PhaseMarkers, registration.ServiceTypes[s]) >= 0)
                        phases.Add(registration.ServiceTypes[s].Substring("global::Internal.".Length));
                }

                if (phases.Count < 2)
                    continue;

                graph.Diagnostics.Add(new DiagnosticInfo(
                    GraphDescriptors.TransientPhaseListener,
                    registration.Location,
                    registration.ImplementationType,
                    string.Join(", ", phases),
                    registration.File ?? "",
                    registration.Line.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }
}
