using System;
using System.Collections.Generic;

namespace Internal
{
    // Reads of the container diagnostics for the editor tools: a source that throws is logged and shows a placeholder.
    internal static class ContainerGraphRead
    {
        private static readonly ILog _log = Logs.Of(typeof(ContainerGraphRead));

        public static IReadOnlyList<T> List<T>(Func<IReadOnlyList<T>> read)
        {
            try
            {
                return read() ?? Array.Empty<T>();
            }
            catch (Exception exception)
            {
                _log.Exception(exception);
                return Array.Empty<T>();
            }
        }

        public static IContainerDiagnostics Parent(IContainerDiagnostics diagnostics)
        {
            if (diagnostics == null)
                return null;

            try
            {
                return diagnostics.Parent;
            }
            catch (Exception exception)
            {
                _log.Exception(exception);
                return null;
            }
        }

        public static string Name(IContainerDiagnostics diagnostics)
        {
            if (diagnostics == null)
                return "(none)";

            try
            {
                return string.IsNullOrEmpty(diagnostics.Name) ? "(unnamed)" : diagnostics.Name;
            }
            catch (Exception exception)
            {
                _log.Exception(exception);
                return "(error)";
            }
        }

        public static string TypeName(Type type)
        {
            if (type == null)
                return "—";

            if (type.IsGenericType == false)
                return type.Name;

            var tick = type.Name.IndexOf('`');
            var name = tick >= 0 ? type.Name.Substring(0, tick) : type.Name;
            var arguments = type.GetGenericArguments();
            var inner = new string[arguments.Length];

            for (var i = 0; i < arguments.Length; i++)
                inner[i] = TypeName(arguments[i]);

            return $"{name}<{string.Join(", ", inner)}>";
        }

        public static string Registration(RegistrationInfo info)
        {
            var implementation = TypeName(info.ImplementationType);
            var services = ServiceTypes(info.ServiceTypes);
            return $"[{info.Slot}] {implementation} : {services} · {info.Lifetime}";
        }

        public static string Asset(LoadedAssetInfo info)
        {
            var label = string.IsNullOrEmpty(info.Label) ? "(no label)" : info.Label;
            var group = string.IsNullOrEmpty(info.GroupName) ? "(no group)" : info.GroupName;
            return $"{label} · {group}";
        }

        public static string PathOf(IContainerDiagnostics diagnostics, IContainerDiagnostics parent)
        {
            var name = Name(diagnostics);

            if (parent == null)
                return name;

            return $"{Name(parent)} / {name}";
        }

        public static string ServiceTypes(IReadOnlyList<Type> types)
        {
            if (types == null || types.Count == 0)
                return "—";

            if (types.Count == 1)
                return TypeName(types[0]);

            var names = new string[types.Count];

            for (var i = 0; i < types.Count; i++)
                names[i] = TypeName(types[i]);

            return string.Join(", ", names);
        }
    }
}