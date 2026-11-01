using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal sealed class InstallerClassifier {
        private readonly ReferenceSymbols _references;

        public InstallerClassifier(ReferenceSymbols references) {
            _references = references;
        }

        public bool IsRoot(IMethodSymbol method) {
            if (HasGraphRootAttribute(method))
                return true;

            if (method.Name != "Construct" && method.Name != "Build")
                return false;

            return HasBuilderParameter(method);
        }

        private bool HasGraphRootAttribute(IMethodSymbol method) {
            if (_references.GraphRootAttribute == null)
                return false;

            foreach (var attribute in method.GetAttributes()) {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _references.GraphRootAttribute))
                    return true;
            }

            return false;
        }

        public void AssignParent(IMethodSymbol method, GraphMethod result) {
            var current = method;
            while (current != null) {
                if (TryReadParent(current, result))
                    return;
                current = current.ContainingSymbol as IMethodSymbol;
            }
        }

        private bool TryReadParent(IMethodSymbol method, GraphMethod result) {
            if (_references.ScopeParentAttribute == null)
                return false;

            foreach (var attribute in method.GetAttributes()) {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _references.ScopeParentAttribute) == false)
                    continue;
                if (attribute.ConstructorArguments.Length < 2)
                    continue;
                var type = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
                var name = attribute.ConstructorArguments[1].Value as string;
                if (type == null || string.IsNullOrEmpty(name))
                    continue;
                result.ParentHint = TypeNames.ForMetadata(type) + "." + name;
                result.ParentAssembly = type.ContainingAssembly?.Name ?? "";
                return true;
            }

            return false;
        }

        public static IMethodSymbol Normalize(IMethodSymbol method) {
            if (method.ReducedFrom != null)
                return method.ReducedFrom;
            return method.OriginalDefinition ?? method;
        }

        public bool IsInstaller(IMethodSymbol method) {
            method = Normalize(method);
            if (method.MethodKind != MethodKind.Ordinary &&
                method.MethodKind != MethodKind.LocalFunction &&
                method.MethodKind != MethodKind.ReducedExtension)
                return false;

            // The registry itself (ContainerBuilder.AttachBuilder) takes an IBuilder but registers nothing.
            if (IsContainerRegistry(method.ContainingType))
                return false;

            if (HasBuilderParameter(method))
                return true;
            if (method.MethodKind == MethodKind.ReducedExtension && IsBuilderLike(method.ReceiverType))
                return true;

            if (method.Name == "Register" && method.Parameters.Length == 1 && IsEntityBuilder(method.Parameters[0].Type))
                return true;

            if (method.Name == "Create" && method.Parameters.Length == 1 && IsScopeBuilder(method.Parameters[0].Type))
                return true;

            return false;
        }

        private bool IsEntityOrSceneInstaller(IMethodSymbol method) {
            if (method.IsStatic)
                return false;
            if (method.Name == "Register" && method.Parameters.Length == 1 && IsEntityBuilder(method.Parameters[0].Type))
                return method.ContainingType != null && method.ContainingType.Implements(_references.EntityComponent);
            if (method.Name == "Create" && method.Parameters.Length == 1 && IsScopeBuilder(method.Parameters[0].Type))
                return method.ContainingType != null && method.ContainingType.Implements(_references.SceneService);
            return false;
        }

        public bool ShouldWalkAsInstaller(IMethodSymbol method) {
            if (IsEntityOrSceneInstaller(method))
                return true;
            if (IsPrimitiveBuilder(method))
                return false;
            return IsInstaller(method);
        }

        // Builder primitives carry [Internal.ContainerPrimitive(kind)], see Primitives. FindOrLoadSceneWithServices is
        // handled at the call site but its body is still walked as an installer (it calls SceneServicesFactory.Create),
        // so it stays out of the primitives here.
        public static bool IsPrimitiveBuilder(IMethodSymbol method) {
            var kind = Primitives.OfSymbol(method);
            return kind != PrimitiveKind.None && kind != PrimitiveKind.SceneServices;
        }

        private bool HasBuilderParameter(IMethodSymbol method) {
            foreach (var parameter in method.Parameters) {
                if (IsBuilderLike(parameter.Type))
                    return true;
            }

            return false;
        }

        public bool IsBuilderLike(ITypeSymbol? type) {
            if (type == null)
                return false;
            if (IsScopeBuilder(type) || IsEntityBuilder(type))
                return true;
            if (type.Name == "IBuilder" || type.Name == "IContainerBuilder")
                return true;
            return type.Implements(_references.Builder) || type.Implements(_references.ScopeBuilder) || type.Implements(_references.EntityBuilder);
        }

        public bool IsRegistrationLike(ITypeSymbol? type) {
            if (type == null)
                return false;
            if (type.Name == "IServiceRegistration")
                return true;
            return type.Implements(_references.ServiceRegistration);
        }

        private bool IsContainerRegistry(ITypeSymbol? type) {
            if (type == null)
                return false;
            return type.Name == "IContainerRegistry" || type.Implements(_references.ContainerRegistry);
        }

        private bool IsScopeBuilder(ITypeSymbol type) {
            return type.Name == "IScopeBuilder" || type.Implements(_references.ScopeBuilder);
        }

        private bool IsEntityBuilder(ITypeSymbol type) {
            return type.Name == "IEntityBuilder" || type.Implements(_references.EntityBuilder);
        }
    }
}
