using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    // What the walk of the harvest copy found points to ContainerInstallerHarvest.g.cs, a file nobody can open: the
    // registrations of a taken body and the diagnostics get the location of the original syntax back. The diagnostics
    // of a copy whose body is not taken are dropped: the walk of the original reports its own.
    internal static class HarvestRelocation {
        public static void Registrations(GraphMethod target, ManifestHarvest.Mapping mapping) {
            for (var i = 0; i < target.Registrations.Count; i++) {
                var registration = target.Registrations[i];
                if (registration.Location == null || registration.Location.FilePath != ManifestHarvest.Path)
                    continue;

                var location = Map(mapping, registration.Location);
                target.Registrations[i] = registration.CloneAt(
                    location.SourceTree?.FilePath ?? "",
                    location.GetLineSpan().StartLinePosition.Line + 1,
                    LocationInfo.CreateFrom(location));
            }
        }

        public static void Diagnostics(GraphDocument document, ManifestHarvest harvest, HashSet<string> taken) {
            for (var i = document.Diagnostics.Count - 1; i >= 0; i--) {
                var diagnostic = document.Diagnostics[i];
                if (diagnostic.Location == null || diagnostic.Location.FilePath != ManifestHarvest.Path)
                    continue;

                var mapping = Find(harvest, diagnostic.Location.TextSpan.Start);
                if (mapping == null || taken.Contains(mapping.HarvestName) == false) {
                    document.Diagnostics.RemoveAt(i);
                    continue;
                }

                document.Diagnostics[i] = Relocated(diagnostic, mapping);
            }
        }

        private static DiagnosticInfo Relocated(DiagnosticInfo diagnostic, ManifestHarvest.Mapping mapping) {
            var location = Map(mapping, diagnostic.Location!);
            var args = new string[diagnostic.MessageArgs.Count];
            for (var i = 0; i < args.Length; i++)
                args[i] = diagnostic.MessageArgs[i];

            // WalkDiagnostics.Error: what, the id of the method, file, line.
            if (args.Length == 4) {
                args[1] = mapping.OriginalId;
                args[2] = location.SourceTree?.FilePath ?? "";
                args[3] = (location.GetLineSpan().StartLinePosition.Line + 1).ToString();
            }

            return new DiagnosticInfo(diagnostic.Descriptor, LocationInfo.CreateFrom(location), args);
        }

        private static Location Map(ManifestHarvest.Mapping mapping, LocationInfo location) {
            var segments = mapping.Segments;
            for (var i = 0; i < segments.Count; i++) {
                if (segments[i].Contains(location.TextSpan.Start))
                    return segments[i].Map(location.TextSpan);
            }

            return mapping.OriginalLocation;
        }

        private static ManifestHarvest.Mapping? Find(ManifestHarvest harvest, int position) {
            for (var i = 0; i < harvest.Mappings.Count; i++) {
                var mapping = harvest.Mappings[i];
                if (position >= mapping.BlockStart && position < mapping.BlockEnd)
                    return mapping;
            }

            return null;
        }
    }
}
