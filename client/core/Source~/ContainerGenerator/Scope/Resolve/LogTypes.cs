namespace ContainerGenerator {
    // ILog<T> of the core is given by every scope without a registration: the argument is Logs.Of<T>() (one cached log per
    // type), so a log needs no Register in any installer.
    internal static class LogTypes {
        private const string Prefix = "global::Internal.ILog<";

        public static bool TryArgument(string type, out string argument) {
            argument = "";
            if (type.StartsWith(Prefix, System.StringComparison.Ordinal) == false || type.EndsWith(">", System.StringComparison.Ordinal) == false)
                return false;

            argument = type.Substring(Prefix.Length, type.Length - Prefix.Length - 1);
            return argument.Length > 0;
        }

        public static string Expression(string argument) {
            return "global::Internal.Logs.Of<" + argument + ">()";
        }
    }
}
