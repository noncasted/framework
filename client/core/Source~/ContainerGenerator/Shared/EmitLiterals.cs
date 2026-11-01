using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class EmitLiterals {
        public static string Literal(string value) {
            if (value == null)
                value = "";
            return "@\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public static string TypeArray(List<string> types) {
            if (types.Count == 0)
                return "global::System.Type.EmptyTypes";

            var items = new List<string>();
            for (var i = 0; i < types.Count; i++)
                items.Add("typeof(" + types[i] + ")");
            return "new global::System.Type[] { " + string.Join(", ", items) + " }";
        }
    }
}
