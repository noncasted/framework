using System.Globalization;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class ResolveDiagnostics {
        public static DiagnosticInfo MissingType(GraphRegistration registration, string typeFullName) {
            var line = registration.Line.ToString(CultureInfo.InvariantCulture);
            return new DiagnosticInfo(
                GraphDescriptors.MissingRegistration,
                registration.Location,
                "(implementation)",
                typeFullName,
                string.IsNullOrEmpty(registration.ImplementationType) ? registration.Kind : registration.ImplementationType,
                registration.File ?? "",
                line);
        }

        public static DiagnosticInfo TypeProblem(
            DiagnosticDescriptor descriptor,
            GraphRegistration registration,
            INamedTypeSymbol type,
            string problem) {
            return new DiagnosticInfo(
                descriptor,
                registration.Location,
                TypeNames.ForCode(type),
                problem,
                registration.File ?? "",
                registration.Line.ToString(CultureInfo.InvariantCulture));
        }

        public static DiagnosticInfo NoInjectMethod(GraphRegistration registration) {
            return new DiagnosticInfo(
                GraphDescriptors.InjectableWithoutInject,
                registration.Location,
                registration.ImplementationType,
                registration.File ?? "",
                registration.Line.ToString(CultureInfo.InvariantCulture));
        }

        public static DiagnosticInfo Missing(GraphRegistration registration, ParameterModel parameter) {
            var line = registration.Line.ToString(CultureInfo.InvariantCulture);
            var implementation = string.IsNullOrEmpty(registration.ImplementationType)
                ? registration.Kind
                : registration.ImplementationType;
            return new DiagnosticInfo(
                GraphDescriptors.MissingRegistration,
                registration.Location,
                parameter.Name,
                parameter.TypeFullName,
                implementation,
                registration.File ?? "",
                line);
        }

        public static string Display(GraphRegistration registration) {
            if (string.IsNullOrEmpty(registration.ImplementationType) == false)
                return registration.ImplementationType;
            if (registration.Arms.Count > 0 && string.IsNullOrEmpty(registration.Arms[0].ImplementationType) == false)
                return registration.Arms[0].ImplementationType;
            return registration.Kind;
        }
    }
}
