using System.Globalization;
using System.Text;

namespace ContainerGenerator {
    internal static class ManifestBlobWriter {
        public static string WriteBlob(GraphMethod method) {
            var builder = new StringBuilder();
            builder.Append(ManifestBlobFormat.BlobVersion);
            builder.Append('\n');
            builder.Append("parent:");
            builder.Append(string.IsNullOrEmpty(method.ParentId) ? method.ParentHint : method.ParentId);
            builder.Append('\n');
            builder.Append("calls:");
            for (var i = 0; i < method.CallOrdinals.Count; i++) {
                if (i > 0)
                    builder.Append(',');
                builder.Append(method.CallOrdinals[i].ToString(CultureInfo.InvariantCulture));
            }

            builder.Append('\n');
            builder.Append("return:");
            builder.Append(method.ReturnedOrdinal.ToString(CultureInfo.InvariantCulture));
            builder.Append('\n');
            method.PadCallLists();
            builder.Append("calltypes:");
            for (var i = 0; i < method.Calls.Count; i++) {
                if (i > 0)
                    builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(method.CallTypeArguments[i]));
            }

            builder.Append('\n');
            builder.Append("callas:");
            for (var i = 0; i < method.Calls.Count; i++) {
                if (i > 0)
                    builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(method.CallAsServices[i]));
            }

            builder.Append('\n');
            builder.Append("callholes:");
            for (var i = 0; i < method.Calls.Count; i++) {
                if (i > 0)
                    builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(method.CallHoles[i]));
            }

            builder.Append('\n');
            builder.Append("callparams:");
            for (var i = 0; i < method.Calls.Count; i++) {
                if (i > 0)
                    builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(method.CallParameters[i]));
            }

            builder.Append('\n');
            for (var i = 0; i < method.Registrations.Count; i++) {
                var registration = method.Registrations[i];
                builder.Append(ManifestBlobFormat.Escape(registration.Kind));
                builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(registration.Lifetime));
                builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(registration.Origin));
                builder.Append('|');
                builder.Append(registration.Ordinal.ToString(CultureInfo.InvariantCulture));
                builder.Append('|');
                builder.Append(registration.ServiceTypes.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append('|');
                builder.Append(registration.Arms.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(registration.Hole));
                builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(registration.Source));
                builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(registration.File));
                builder.Append('|');
                builder.Append(registration.Line.ToString(CultureInfo.InvariantCulture));
                builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(registration.ImplementationType));
                builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(registration.TypeMap));
                builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(string.Join(";", registration.ServiceMaps.ToArray())));
                builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(registration.BranchPath));
                builder.Append('|');
                builder.Append(ManifestBlobFormat.Escape(string.Join(";", registration.ParameterTypes.ToArray())));
                builder.Append('\n');
                for (var a = 0; a < registration.Arms.Count; a++) {
                    var arm = registration.Arms[a];
                    builder.Append(ManifestBlobFormat.Escape(arm.Discriminant));
                    builder.Append('|');
                    builder.Append(ManifestBlobFormat.Escape(arm.ImplementationType));
                    builder.Append('|');
                    builder.Append(ManifestBlobFormat.Escape(arm.ParameterExpression));
                    builder.Append('|');
                    builder.Append(ManifestBlobFormat.Escape(arm.ParameterType));
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }
    }
}
