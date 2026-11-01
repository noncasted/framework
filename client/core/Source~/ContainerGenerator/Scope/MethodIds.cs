using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class MethodIds {
        // Type.Method; a local function: Outer+Local. Overloads of an installer also carry their parameter types
        // (Type.Method(Sample.IBuilder, int)): the bodies of the overloads are different methods of the graph. A root
        // keeps the bare name, the runtime finds it by that (GeneratedScopes.RootId); an overloaded root is CINGR013.
        public static string Of(IMethodSymbol method) {
            if (method.MethodKind == MethodKind.LocalFunction && method.ContainingSymbol is IMethodSymbol parent)
                return Of(parent) + "+" + method.Name;

            var definition = method.ReducedFrom ?? method.OriginalDefinition ?? method;
            var type = definition.ContainingType != null ? TypeNames.ForMetadata(definition.ContainingType) : "";
            var name = string.IsNullOrEmpty(type) ? definition.Name : type + "." + definition.Name;
            var suffix = OverloadSuffix(definition);
            if (method.TypeArguments.Length == 0)
                return name + suffix;

            var arguments = new string[method.TypeArguments.Length];
            for (var i = 0; i < method.TypeArguments.Length; i++)
                arguments[i] = TypeNames.ForMetadata(method.TypeArguments[i]);
            return name + "<" + string.Join(", ", arguments) + ">" + suffix;
        }

        public static bool HasOverloadSuffix(string id) {
            return id.IndexOf('(') >= 0;
        }

        public static IMethodSymbol? Find(Compilation compilation, string rootId) {
            if (compilation == null || string.IsNullOrEmpty(rootId))
                return null;

            foreach (var tree in compilation.SyntaxTrees) {
                var model = compilation.GetSemanticModel(tree);
                foreach (var node in tree.GetRoot().DescendantNodes()) {
                    IMethodSymbol? method = null;
                    if (node is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax methodSyntax)
                        method = model.GetDeclaredSymbol(methodSyntax) as IMethodSymbol;
                    else if (node is Microsoft.CodeAnalysis.CSharp.Syntax.LocalFunctionStatementSyntax localSyntax)
                        method = model.GetDeclaredSymbol(localSyntax) as IMethodSymbol;

                    if (method == null)
                        continue;
                    if (Of(method) == rootId)
                        return method;
                }
            }

            return null;
        }

        // Another assembly sees only part of the overloads (public ones, internal ones with InternalsVisibleTo), and
        // both sides must get one id: a method compares itself only with the overloads visible wherever it is visible.
        // At most one overload of a type stays without the suffix, the most visible one.
        private static string OverloadSuffix(IMethodSymbol definition) {
            if (definition.ContainingType == null || IsRootLike(definition) || IsGraphMethod(definition) == false)
                return "";

            var visibility = Visibility(definition);
            foreach (var member in definition.ContainingType.GetMembers(definition.Name)) {
                if (member is not IMethodSymbol other || other.MethodKind != MethodKind.Ordinary)
                    continue;
                if (SymbolEqualityComparer.Default.Equals(other, definition) || IsGraphMethod(other) == false)
                    continue;
                if (Visibility(other) > visibility && IsRootLike(other) == false)
                    continue;

                var parameters = new List<string>();
                foreach (var parameter in definition.Parameters)
                    parameters.Add(TypeNames.ForMetadata(parameter.Type));
                return "(" + string.Join(", ", parameters) + ")";
            }

            return "";
        }

        private static int Visibility(IMethodSymbol method) {
            switch (method.DeclaredAccessibility) {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    return 0;
                case Accessibility.Internal:
                case Accessibility.ProtectedAndInternal:
                    return 1;
                default:
                    return 2;
            }
        }

        private static bool IsRootLike(IMethodSymbol method) {
            foreach (var attribute in method.GetAttributes()) {
                if (attribute.AttributeClass?.ToDisplayString() == "Internal.ContainerGraphRootAttribute")
                    return true;
            }

            if (method.Name != "Construct" && method.Name != "Build")
                return false;
            foreach (var parameter in method.Parameters) {
                if (IsBuilder(parameter.Type))
                    return true;
            }

            return false;
        }

        // What can be an installer: a method that takes a builder or a registration.
        private static bool IsGraphMethod(IMethodSymbol method) {
            foreach (var parameter in method.Parameters) {
                if (IsBuilder(parameter.Type) || Is(parameter.Type, "IServiceRegistration"))
                    return true;
            }

            return false;
        }

        private static bool IsBuilder(ITypeSymbol type) {
            return Is(type, "IBuilder") || Is(type, "IScopeBuilder") || Is(type, "IEntityBuilder") || Is(type, "IContainerBuilder");
        }

        private static bool Is(ITypeSymbol type, string name) {
            if (type.Name == name)
                return true;
            foreach (var implemented in type.AllInterfaces) {
                if (implemented.Name == name)
                    return true;
            }

            return false;
        }
    }
}
