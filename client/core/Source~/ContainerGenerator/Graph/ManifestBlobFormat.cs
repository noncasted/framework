using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ContainerGenerator {
    internal static class ManifestBlobFormat {
        public const string BlobVersion = "v2";
        public const string VoidSentinel = "global::System.Void";

        public static string Escape(string value) {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Replace("\\", "\\\\").Replace("|", "\\|").Replace("\r", "").Replace("\n", "\\n");
        }

        public static string Unescape(string value) {
            if (string.IsNullOrEmpty(value))
                return "";

            var builder = new StringBuilder();
            for (var i = 0; i < value.Length; i++) {
                if (value[i] == '\\' && i + 1 < value.Length) {
                    var next = value[i + 1];
                    if (next == '\\' || next == '|') {
                        builder.Append(next);
                        i++;
                        continue;
                    }

                    if (next == 'n') {
                        builder.Append('\n');
                        i++;
                        continue;
                    }
                }

                builder.Append(value[i]);
            }

            return builder.ToString();
        }

        public static string[] SplitFields(string line) {
            var fields = new List<string>();
            var current = new StringBuilder();
            for (var i = 0; i < line.Length; i++) {
                if (line[i] == '\\' && i + 1 < line.Length) {
                    current.Append('\\');
                    current.Append(line[i + 1]);
                    i++;
                    continue;
                }

                if (line[i] == '|') {
                    fields.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(line[i]);
            }

            fields.Add(current.ToString());
            return fields.ToArray();
        }
    }
}
