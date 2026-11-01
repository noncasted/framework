using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    // A compilation does not import the internal and private members of a referenced assembly, so an [Inject] method of
    // such a type is not there and the scope would silently not call it. The members are read again from a copy of the
    // compilation that imports everything, only for the types that came from metadata.
    internal static class HiddenInject {
        private static readonly ConditionalWeakTable<Compilation, Lookup> Lookups = new ConditionalWeakTable<Compilation, Lookup>();

        public static List<string> Problems(INamedTypeSymbol type, Compilation compilation) {
            var problems = new List<string>();
            for (var current = type; current != null; current = current.BaseType) {
                if (current.DeclaringSyntaxReferences.Length > 0 || current.SpecialType == SpecialType.System_Object ||
                    current.TypeKind == TypeKind.Error)
                    continue;

                foreach (var name in Lookups.GetValue(compilation, c => new Lookup(c)).MethodsOf(current)) {
                    problems.Add("its [Inject] method '" + name + "' of the assembly '" + current.ContainingAssembly.Name +
                                 "' is not visible from here, the scope cannot call it (make it public)");
                }
            }

            return problems;
        }

        public static bool IsInjectAttribute(INamedTypeSymbol attribute) {
            return attribute.Name == "InjectAttribute" && attribute.ContainingNamespace?.ToDisplayString() == "Internal";
        }

        private sealed class Lookup {
            private readonly Compilation _all;
            private readonly Dictionary<INamedTypeSymbol, List<string>> _methods =
                new Dictionary<INamedTypeSymbol, List<string>>(SymbolEqualityComparer.Default);

            public Lookup(Compilation compilation) {
                _all = compilation.WithOptions(compilation.Options.WithMetadataImportOptions(MetadataImportOptions.All));
            }

            public List<string> MethodsOf(INamedTypeSymbol type) {
                var definition = type.OriginalDefinition;
                if (_methods.TryGetValue(definition, out var methods))
                    return methods;

                methods = new List<string>();
                foreach (var member in FindWithAllMembers(definition).GetMembers()) {
                    if (member is not IMethodSymbol method || method.MethodKind != MethodKind.Ordinary || method.IsStatic)
                        continue;
                    if (HasInject(method) && IsVisible(definition, method) == false)
                        methods.Add(method.Name);
                }

                _methods.Add(definition, methods);
                return methods;
            }

            private INamedTypeSymbol FindWithAllMembers(INamedTypeSymbol type) {
                foreach (var assembly in _all.SourceModule.ReferencedAssemblySymbols) {
                    if (assembly.Name != type.ContainingAssembly.Name)
                        continue;
                    var found = assembly.GetTypeByMetadataName(MetadataName(type));
                    if (found != null)
                        return found;
                }

                throw new System.InvalidOperationException(
                    "Type '" + MetadataName(type) + "' is not found in the assembly '" + type.ContainingAssembly.Name + "'.");
            }

            private static bool IsVisible(INamedTypeSymbol type, IMethodSymbol method) {
                var signature = method.ToDisplayString();
                foreach (var member in type.GetMembers(method.Name)) {
                    if (member.ToDisplayString() == signature)
                        return true;
                }

                return false;
            }

            private static bool HasInject(IMethodSymbol method) {
                foreach (var attribute in method.GetAttributes()) {
                    if (attribute.AttributeClass != null && IsInjectAttribute(attribute.AttributeClass))
                        return true;
                }

                return false;
            }

            private static string MetadataName(INamedTypeSymbol type) {
                var name = type.MetadataName;
                for (var outer = type.ContainingType; outer != null; outer = outer.ContainingType)
                    name = outer.MetadataName + "+" + name;
                var ns = type.ContainingNamespace;
                return ns == null || ns.IsGlobalNamespace ? name : ns.ToDisplayString() + "." + name;
            }
        }
    }
}
