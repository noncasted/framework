using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class PlanDiagnostics {
        // The table for the container graph: the own registrations by slot, then the holes of the edges: what
        // the scope takes from the parent or from WithParameter.
        public static void BuildDiagnostics(ScopePlan plan, Compilation compilation, TypeIndex types) {
            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                var registration = slot.Registration;
                var entry = new ScopePlan.DiagnosticRegistration {
                    ImplementationType = DiagnosticType(compilation, types, slot.ImplementationType),
                    Lifetime = registration.Lifetime,
                    IsInstantiated = slot.IsTransient == false && registration.Origin != "Injectable",
                };

                for (var s = 0; s < registration.ServiceTypes.Count; s++)
                    AddDistinct(entry.ServiceTypes, DiagnosticType(compilation, types, registration.ServiceTypes[s]));

                for (var e = 0; e < registration.Dependencies.Count; e++) {
                    var edge = registration.Dependencies[e];
                    if (edge.Kind == "Registration")
                        AddDependency(plan, entry.Dependencies, edge.TargetIndex);
                    else if (edge.Kind == "Collection") {
                        for (var c = 0; c < edge.CollectionIndices.Count; c++)
                            AddDependency(plan, entry.Dependencies, edge.CollectionIndices[c]);
                    }
                }

                plan.DiagnosticRegistrations.Add(entry);
            }

            for (var i = 0; i < plan.CtorParams.Count; i++) {
                var param = plan.CtorParams[i];
                if (string.IsNullOrEmpty(param.FieldName))
                    continue;

                var type = DiagnosticType(compilation, types, param.Type);
                var entry = new ScopePlan.DiagnosticRegistration {
                    ImplementationType = type,
                    IsInstantiated = true,
                    IsExternal = true,
                };
                AddDistinct(entry.ServiceTypes, type);
                plan.DiagnosticRegistrations.Add(entry);
            }
        }

        private static string DiagnosticType(Compilation compilation, TypeIndex types, string type) {
            if (string.IsNullOrEmpty(type) || type.IndexOf('{') >= 0)
                return "";

            var symbol = types.Find(type);
            if (symbol == null || symbol.IsUnboundGenericType || ContainsTypeParameter(symbol))
                return "";
            if (compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly) == false)
                return "";
            return TypeNames.ForCode(symbol);
        }

        private static bool ContainsTypeParameter(ITypeSymbol type) {
            if (type is ITypeParameterSymbol)
                return true;
            if (type is IArrayTypeSymbol array)
                return ContainsTypeParameter(array.ElementType);
            if (type is INamedTypeSymbol named) {
                for (var i = 0; i < named.TypeArguments.Length; i++) {
                    if (ContainsTypeParameter(named.TypeArguments[i]))
                        return true;
                }
            }

            return false;
        }

        private static void AddDependency(ScopePlan plan, List<int> dependencies, int index) {
            if (index < 0 || index >= plan.Slots.Count || dependencies.Contains(index))
                return;
            dependencies.Add(index);
        }

        private static void AddDistinct(List<string> list, string value) {
            if (string.IsNullOrEmpty(value) || list.Contains(value))
                return;
            list.Add(value);
        }
    }
}
