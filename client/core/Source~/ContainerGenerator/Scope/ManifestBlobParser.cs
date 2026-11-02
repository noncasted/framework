using System;
using System.Collections.Generic;
using System.Globalization;

namespace ContainerGenerator {
    internal static class ManifestBlobParser {
        public static void ParseBlob(
            GraphMethod method,
            string blob,
            List<string> implementations,
            List<string> services,
            string assemblyName) {
            var lines = (blob ?? "").Replace("\r", "").Split('\n');

            // A manifest that is not read loses the registrations of the installer without a word.
            if (lines[0] != ManifestBlobFormat.BlobVersion)
                throw new InvalidOperationException(
                    "The manifest of '" + method.Id + "' in assembly '" + assemblyName + "' has version '" + lines[0] +
                    "', this generator reads '" + ManifestBlobFormat.BlobVersion + "': rebuild that assembly with this generator.");

            var index = 1;
            if (index < lines.Length && lines[index].StartsWith("parent:", StringComparison.Ordinal)) {
                method.SetParentId(lines[index].Substring("parent:".Length));
                method.SetParentHint(method.ParentId);
                index++;
            }

            if (index < lines.Length && lines[index].StartsWith("calls:", StringComparison.Ordinal)) {
                var raw = lines[index].Substring("calls:".Length);
                if (string.IsNullOrEmpty(raw) == false) {
                    var parts = raw.Split(',');
                    for (var i = 0; i < parts.Length; i++) {
                        if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ordinal))
                            method.CallOrdinals.Add(ordinal);
                    }
                }

                index++;
            }

            if (index < lines.Length && lines[index].StartsWith("return:", StringComparison.Ordinal)) {
                method.SetReturnedOrdinal(ParseInt(lines[index].Substring("return:".Length)));
                index++;
            }

            if (index < lines.Length && lines[index].StartsWith("calltypes:", StringComparison.Ordinal)) {
                SplitCallField(method.CallTypeArguments, lines[index].Substring("calltypes:".Length));
                index++;
            }

            if (index < lines.Length && lines[index].StartsWith("callas:", StringComparison.Ordinal)) {
                SplitCallField(method.CallAsServices, lines[index].Substring("callas:".Length));
                index++;
            }

            if (index < lines.Length && lines[index].StartsWith("callholes:", StringComparison.Ordinal)) {
                SplitCallField(method.CallHoles, lines[index].Substring("callholes:".Length));
                index++;
            }

            if (index < lines.Length && lines[index].StartsWith("callparams:", StringComparison.Ordinal)) {
                SplitCallField(method.CallParameters, lines[index].Substring("callparams:".Length));
                index++;
            }

            var serviceCursor = 0;
            var implementationCursor = 0;
            while (index < lines.Length) {
                var line = lines[index];
                index++;
                if (string.IsNullOrEmpty(line))
                    continue;

                var fields = ManifestBlobFormat.SplitFields(line);
                if (fields.Length != 15)
                    throw new InvalidOperationException(
                        "The manifest of '" + method.Id + "' in assembly '" + assemblyName + "' has a broken registration: " + line);

                var serviceCount = ParseInt(fields[4]);
                var armCount = ParseInt(fields[5]);
                var implementation = ManifestBlobFormat.Unescape(fields[10]);
                if (string.IsNullOrEmpty(implementation) && implementationCursor < implementations.Count)
                    implementation = implementations[implementationCursor];
                // The array has typeof(void) for what typeof cannot write (a switch has no implementation of its own).
                if (implementation == ManifestBlobFormat.VoidSentinel || implementation == "void")
                    implementation = "";
                implementationCursor++;

                var registration = new GraphRegistration {
                    Kind = ManifestBlobFormat.Unescape(fields[0]),
                    Lifetime = ManifestBlobFormat.Unescape(fields[1]),
                    Origin = ManifestBlobFormat.Unescape(fields[2]),
                    Ordinal = ParseInt(fields[3]),
                    Hole = ManifestBlobFormat.Unescape(fields[6]),
                    Source = ManifestBlobFormat.Unescape(fields[7]),
                    File = ManifestBlobFormat.Unescape(fields[8]),
                    Line = ParseInt(fields[9]),
                    ImplementationType = implementation,
                    TypeMap = ManifestBlobFormat.Unescape(fields[11]),
                    BranchPath = ManifestBlobFormat.Unescape(fields[13]),
                };

                var maps = ManifestBlobFormat.Unescape(fields[12]).Split(';');
                for (var m = 0; m < maps.Length; m++)
                    registration.ServiceMaps.Add(maps[m]);

                var parameterTypes = ManifestBlobFormat.Unescape(fields[14]);
                if (string.IsNullOrEmpty(parameterTypes) == false)
                    registration.ParameterTypes.AddRange(parameterTypes.Split(';'));

                for (var s = 0; s < serviceCount; s++) {
                    var service = "";
                    if (serviceCursor < services.Count)
                        service = services[serviceCursor];
                    serviceCursor++;
                    if (service == ManifestBlobFormat.VoidSentinel)
                        service = "";
                    if (string.IsNullOrEmpty(service) == false)
                        registration.ServiceTypes.Add(service);
                }

                for (var a = 0; a < armCount && index < lines.Length; a++) {
                    var armFields = ManifestBlobFormat.SplitFields(lines[index]);
                    index++;
                    var arm = new GraphSwitchArm {
                        Discriminant = armFields.Length > 0 ? ManifestBlobFormat.Unescape(armFields[0]) : "",
                        ImplementationType = armFields.Length > 1 ? ManifestBlobFormat.Unescape(armFields[1]) : "",
                    };
                    arm.SetParameter(
                        armFields.Length > 2 ? ManifestBlobFormat.Unescape(armFields[2]) : "",
                        armFields.Length > 3 ? ManifestBlobFormat.Unescape(armFields[3]) : "");
                    registration.Arms.Add(arm);
                }

                method.Registrations.Add(registration);
            }
        }

        private static int ParseInt(string value) {
            if (int.TryParse(ManifestBlobFormat.Unescape(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return 0;
        }

        private static void SplitCallField(List<string> target, string raw) {
            if (string.IsNullOrEmpty(raw))
                return;
            var parts = raw.Split('|');
            for (var i = 0; i < parts.Length; i++)
                target.Add(ManifestBlobFormat.Unescape(parts[i]));
        }
    }
}
