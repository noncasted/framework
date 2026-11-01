using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    // What a type needs: the parameters of its constructor and of its [Inject] method. EdgeResolver builds the edges.
    // The inject method is found only by Internal.InjectAttribute (on the type or a base class), its name does not matter.
    internal static class TypeAnalyzer {
        public static InjectorModel? Analyze(
            INamedTypeSymbol type,
            ReferenceSymbols references,
            LocationInfo? location,
            bool requireConstructOrExplicitCtor = true) {
            if (type.TypeKind != TypeKind.Class)
                return null;
            if (type.IsStatic || type.IsAbstract)
                return null;
            if (type.IsImplicitClass)
                return null;
            if (InheritsAttribute(type))
                return null;

            var constructMethods = InjectMethods(type, references);

            IMethodSymbol? construct = null;
            var multipleConstruct = constructMethods.Count > 1;
            if (multipleConstruct == false && constructMethods.Count == 1)
                construct = constructMethods[0];

            IMethodSymbol? constructor = SelectConstructor(type);
            var hasConstruct = construct != null;
            var hasExplicitCtor = constructor != null && constructor.IsImplicitlyDeclared == false;

            if (hasConstruct == false && hasExplicitCtor == false && requireConstructOrExplicitCtor)
                return null;

            var constructorParameters = EquatableArray<ParameterModel>.Empty;
            if (constructor != null && constructor.CanCallFromSameAssembly())
                constructorParameters = ToParameters(constructor);

            var constructParameters = EquatableArray<ParameterModel>.Empty;
            if (construct != null)
                constructParameters = ToParameters(construct);

            var ns = type.ContainingNamespace;
            return new InjectorModel(
                type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                TypeNames.ForCode(type),
                ns == null || ns.IsGlobalNamespace ? "" : ns.ToDisplayString(),
                type.Name,
                type.ContainingType != null,
                type.IsAbstract,
                type.IsGenericType,
                references.UnityObject != null && type.InheritsFrom(references.UnityObject),
                hasConstruct,
                construct != null && construct.CanCallFromSameAssembly(),
                construct != null && construct.Arity > 0,
                multipleConstruct,
                constructor != null,
                constructor != null && constructor.CanCallFromSameAssembly(),
                constructorParameters,
                constructParameters,
                construct != null ? construct.Name : "",
                location);
        }

        // The [Inject] methods of the type and its base classes. An override is the method it overrides: it is
        // injected when it or an overridden method has the attribute, and counts once.
        public static List<IMethodSymbol> InjectMethods(INamedTypeSymbol type, ReferenceSymbols references) {
            var result = new List<IMethodSymbol>();
            var overridden = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            for (var current = type; current != null; current = current.BaseType) {
                foreach (var member in current.GetMembers()) {
                    if (member is not IMethodSymbol method)
                        continue;
                    if (method.MethodKind != MethodKind.Ordinary || method.IsStatic)
                        continue;
                    if (overridden.Contains(method))
                        continue;

                    var isInject = HasInjectAttribute(method, references);
                    for (var chain = method.OverriddenMethod; chain != null; chain = chain.OverriddenMethod) {
                        overridden.Add(chain);
                        isInject |= HasInjectAttribute(chain, references);
                    }

                    if (isInject)
                        result.Add(method);
                }
            }

            return result;
        }

        // Why the generated scope cannot call the [Inject] method of the type: an empty list when it can.
        public static List<string> InjectProblems(INamedTypeSymbol type, ReferenceSymbols references, Compilation compilation) {
            var problems = HiddenInject.Problems(type, compilation);
            if (problems.Count > 0)
                return problems;

            var methods = InjectMethods(type, references);
            if (methods.Count > 1) {
                var names = new List<string>();
                for (var i = 0; i < methods.Count; i++)
                    names.Add(methods[i].ContainingType.Name + "." + methods[i].Name);
                problems.Add("it has several [Inject] methods (" + string.Join(", ", names) + "), the scope calls one");
                return problems;
            }

            if (methods.Count == 0)
                return problems;

            var inject = methods[0];
            if (inject.Arity > 0)
                problems.Add("its [Inject] method '" + inject.Name + "' is generic");
            if (compilation.IsSymbolAccessibleWithin(inject, compilation.Assembly) == false)
                problems.Add("its [Inject] method '" + inject.Name + "' is not accessible from the generated scope (make it public)");
            return problems;
        }

        // Why the generated scope cannot construct the type: an empty list when it can.
        public static List<string> ConstructProblems(INamedTypeSymbol type, Compilation compilation) {
            var problems = new List<string>();
            if (type.TypeKind == TypeKind.Struct) {
                problems.Add("it is a struct, the scope constructs classes only (register an instance instead)");
                return problems;
            }

            if (type.TypeKind != TypeKind.Class) {
                problems.Add("it is " + (type.TypeKind == TypeKind.Interface ? "an interface" : "a " + type.TypeKind.ToString().ToLowerInvariant()) +
                             ", register its implementation");
                return problems;
            }

            if (type.IsStatic)
                problems.Add("it is static");
            else if (type.IsAbstract)
                problems.Add("it is abstract, register a concrete type");
            if (compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) == false)
                problems.Add("it is not accessible from the generated scope (make it internal or public)");
            if (problems.Count > 0)
                return problems;

            var longest = -1;
            var count = 0;
            foreach (var constructor in type.InstanceConstructors) {
                if (compilation.IsSymbolAccessibleWithin(constructor, compilation.Assembly) == false)
                    continue;
                if (constructor.Parameters.Length > longest) {
                    longest = constructor.Parameters.Length;
                    count = 1;
                }
                else if (constructor.Parameters.Length == longest) {
                    count++;
                }
            }

            if (longest < 0)
                problems.Add("it has no constructor accessible from the generated scope");
            else if (count > 1)
                problems.Add("it has " + count + " constructors with " + longest + " parameters, the scope takes the longest one (leave one)");
            return problems;
        }

        private static bool HasInjectAttribute(IMethodSymbol method, ReferenceSymbols references) {
            foreach (var attribute in method.GetAttributes()) {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass == null)
                    continue;
                if (references.InjectAttribute != null &&
                    SymbolEqualityComparer.Default.Equals(attributeClass, references.InjectAttribute))
                    return true;
                if (HiddenInject.IsInjectAttribute(attributeClass))
                    return true;
            }

            return false;
        }

        private static bool InheritsAttribute(INamedTypeSymbol type) {
            var current = type;
            while (current != null) {
                if (current.Name == "Attribute" && current.ContainingNamespace?.ToDisplayString() == "System")
                    return true;

                current = current.BaseType;
            }

            return false;
        }

        private static IMethodSymbol? SelectConstructor(INamedTypeSymbol type) {
            IMethodSymbol? best = null;
            foreach (var ctor in type.InstanceConstructors) {
                if (ctor.IsStatic)
                    continue;
                if (ctor.CanCallFromSameAssembly() == false)
                    continue;

                if (best == null || ctor.Parameters.Length > best.Parameters.Length)
                    best = ctor;
            }

            return best;
        }

        private static EquatableArray<ParameterModel> ToParameters(IMethodSymbol method) {
            if (method.Parameters.Length == 0)
                return EquatableArray<ParameterModel>.Empty;

            var items = new ParameterModel[method.Parameters.Length];
            for (var i = 0; i < method.Parameters.Length; i++) {
                var parameter = method.Parameters[i];
                items[i] = new ParameterModel(TypeNames.ForCode(parameter.Type), parameter.Name);
            }

            return new EquatableArray<ParameterModel>(items);
        }
    }
}
