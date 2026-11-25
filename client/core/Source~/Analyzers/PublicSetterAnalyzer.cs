using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Framework.Analyzers
{
    // FWR001: a public set accessor or a public mutable field (CODE_STYLE_FULL.md, "Data Changes Only Through Methods").
    // A public member of an internal type counts; a member of a private nested type does not (only its outer type sees
    // it). The exceptions are the list of the doc: [MemoryPackable] types, [Serializable] types without logic (public
    // fields only: JsonUtility sees no properties), Blazor parameters, attribute arguments, overrides, implementations
    // of an interface member with a setter (an interface of the sources is reported on its own declaration).
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PublicSetterAnalyzer : DiagnosticAnalyzer
    {
        private static readonly string[] _blazorParameters =
        {
            "ParameterAttribute",
            "CascadingParameterAttribute",
            "SupplyParameterFromQueryAttribute",
            "SupplyParameterFromFormAttribute"
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Rules.PublicSetter);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        }

        private static void AnalyzeProperty(SymbolAnalysisContext context)
        {
            var property = (IPropertySymbol)context.Symbol;
            var setter = property.SetMethod;

            if (setter == null || setter.IsInitOnly || property.IsImplicitlyDeclared)
                return;

            if (setter.DeclaredAccessibility != Accessibility.Public || IsVisible(property) == false)
                return;

            if (property.IsOverride || ImplementsInterfaceSetter(property))
                return;

            if (IsExemptType(property.ContainingType) || IsBlazorParameter(property))
                return;

            var location = setter.Locations.FirstOrDefault() ?? property.Locations[0];
            context.ReportDiagnostic(Diagnostic.Create(Rules.PublicSetter, location, "Property", property.Name,
                "{ get; private set; } or { get; init; }"));
        }

        private static void AnalyzeField(SymbolAnalysisContext context)
        {
            var field = (IFieldSymbol)context.Symbol;

            if (field.IsConst || field.IsReadOnly || field.IsImplicitlyDeclared)
                return;

            if (field.DeclaredAccessibility != Accessibility.Public || IsVisible(field) == false)
                return;

            if (IsExemptType(field.ContainingType) || IsJsonUtilityData(field.ContainingType))
                return;

            context.ReportDiagnostic(Diagnostic.Create(Rules.PublicSetter, field.Locations[0], "Field", field.Name,
                "readonly or private ([SerializeField] private for the inspector)"));
        }

        // Not visible outside its outer type when any containing type is private.
        private static bool IsVisible(ISymbol symbol)
        {
            for (var type = symbol.ContainingType; type != null; type = type.ContainingType)
            {
                if (type.DeclaredAccessibility == Accessibility.Private)
                    return false;
            }

            return true;
        }

        private static bool ImplementsInterfaceSetter(IPropertySymbol property)
        {
            var type = property.ContainingType;

            foreach (var implemented in type.AllInterfaces)
            {
                foreach (var member in implemented.GetMembers(property.Name))
                {
                    if (member is not IPropertySymbol interfaceProperty || interfaceProperty.SetMethod == null)
                        continue;

                    var implementation = type.FindImplementationForInterfaceMember(interfaceProperty);

                    if (SymbolEqualityComparer.Default.Equals(implementation, property))
                        return true;
                }
            }

            return false;
        }

        // [MemoryPackable] types (network payloads) and attributes (a named argument needs a public setter), the outer
        // types included.
        private static bool IsExemptType(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.ContainingType)
            {
                if (HasAttribute(current, "MemoryPackableAttribute") || IsAttribute(current))
                    return true;
            }

            return false;
        }

        private static bool IsAttribute(INamedTypeSymbol type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (current.ToDisplayString() == "System.Attribute")
                    return true;
            }

            return false;
        }

        private static bool IsBlazorParameter(IPropertySymbol property)
        {
            return property.GetAttributes().Any(attribute =>
                attribute.AttributeClass != null && _blazorParameters.Contains(attribute.AttributeClass.Name));
        }

        // A [Serializable] type without logic: no methods or operators of its own, constructors and computed getters
        // allowed. Such a type is the format of a JsonUtility file; one with logic keeps [SerializeField] private fields.
        private static bool IsJsonUtilityData(INamedTypeSymbol type)
        {
            if (type.IsSerializable == false && HasAttribute(type, "SerializableAttribute") == false)
                return false;

            foreach (var member in type.GetMembers())
            {
                if (member is not IMethodSymbol method || method.IsImplicitlyDeclared)
                    continue;

                if (method.MethodKind == MethodKind.Ordinary || method.MethodKind == MethodKind.UserDefinedOperator ||
                    method.MethodKind == MethodKind.Conversion)
                    return false;
            }

            return true;
        }

        private static bool HasAttribute(INamedTypeSymbol type, string name)
        {
            return type.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == name);
        }
    }
}
