using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Framework.Analyzers
{
    // FWR005: lock on a value whose type is a lifetime (Internal.IReadOnlyLifetime or a type implementing it) outside
    // the class Internal.Lifetime, which locks on itself. A compilation without the reactive primitives is skipped.
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class LifetimeLockAnalyzer : DiagnosticAnalyzer
    {
        public const string LifetimeInterface = "Internal.IReadOnlyLifetime";
        public const string LifetimeClass = "Internal.Lifetime";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Rules.LifetimeLock);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var lifetimeInterface = start.Compilation.GetTypeByMetadataName(LifetimeInterface);

                if (lifetimeInterface == null)
                    return;

                var lifetimeClass = start.Compilation.GetTypeByMetadataName(LifetimeClass);
                start.RegisterOperationAction(
                    operation => Analyze(operation, lifetimeInterface, lifetimeClass),
                    OperationKind.Lock);
            });
        }

        private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol lifetimeInterface, INamedTypeSymbol? lifetimeClass)
        {
            var lockOperation = (ILockOperation)context.Operation;
            var value = lockOperation.LockedValue;
            var type = value.Type;

            if (type == null || IsLifetime(type, lifetimeInterface) == false)
                return;

            if (lifetimeClass != null && IsInside(context.ContainingSymbol, lifetimeClass))
                return;

            context.ReportDiagnostic(Diagnostic.Create(Rules.LifetimeLock, value.Syntax.GetLocation(), value.Syntax.ToString()));
        }

        private static bool IsLifetime(ITypeSymbol type, INamedTypeSymbol lifetimeInterface)
        {
            if (SymbolEqualityComparer.Default.Equals(type, lifetimeInterface))
                return true;

            // A type parameter lists its interfaces in the constraints, not in AllInterfaces.
            if (type is ITypeParameterSymbol parameter)
            {
                foreach (var constraint in parameter.ConstraintTypes)
                {
                    if (IsLifetime(constraint, lifetimeInterface))
                        return true;
                }
            }

            foreach (var implemented in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(implemented, lifetimeInterface))
                    return true;
            }

            return false;
        }

        private static bool IsInside(ISymbol symbol, INamedTypeSymbol type)
        {
            for (var current = symbol.ContainingType; current != null; current = current.ContainingType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, type))
                    return true;
            }

            return false;
        }
    }
}
