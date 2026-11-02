using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class WalkState {
        public IMethodSymbol Method { get; }
        public SemanticModel Model { get; }
        public GraphMethod Result { get; }
        public Dictionary<string, WalkLocal> Locals { get; } = new Dictionary<string, WalkLocal>(StringComparer.Ordinal);
        public HashSet<string> TaintedBuilder { get; } = new HashSet<string>(StringComparer.Ordinal);
        public Dictionary<ITypeParameterSymbol, ITypeSymbol> Substitution { get; } = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        public Dictionary<string, bool> Bools { get; } = new Dictionary<string, bool>(StringComparer.Ordinal);
        public GraphRegistration? LastRegistration { get; private set; }
        public InvocationExpressionSyntax? LastInstantiate { get; private set; }
        public int LastCallIndex { get; private set; } = -1;
        public List<string> Branches { get; } = new List<string>();
        public int NextBranch { get; private set; }
        public int NextOrdinal { get; private set; }
        public bool EntityRoot { get; init; }

        public WalkState(IMethodSymbol method, SemanticModel model, GraphMethod result) {
            Method = method;
            Model = model;
            Result = result;
        }

        public int TakeBranch() {
            return NextBranch++;
        }

        public int TakeOrdinal() {
            return NextOrdinal++;
        }

        public void RememberRegistration(GraphRegistration? registration) {
            LastRegistration = registration;
            LastCallIndex = -1;
        }

        public void SetLastRegistration(GraphRegistration? registration) {
            LastRegistration = registration;
        }

        public void SetLastCall(int index) {
            LastCallIndex = index;
        }

        public void SetLastInstantiate(InvocationExpressionSyntax invocation) {
            LastInstantiate = invocation;
        }

        // A local names its registration or its installer call; LastRegistration is only for a chain without one.
        public GraphRegistration? CurrentRegistration(InvocationExpressionSyntax invocation) {
            var receiver = WalkSyntax.GetReceiver(invocation);
            if (receiver != null &&
                WalkSyntax.Unwrap(receiver) is IdentifierNameSyntax identifier &&
                Locals.TryGetValue(identifier.Identifier.Text, out var local)) {
                if (local.Registration != null) {
                    LastRegistration = local.Registration;
                    LastCallIndex = -1;
                    return local.Registration;
                }

                if (local.ReturnedCallIndex >= 0) {
                    LastRegistration = null;
                    LastCallIndex = local.ReturnedCallIndex;
                    return null;
                }
            }

            return LastRegistration;
        }

        public string Format(ITypeSymbol? type) {
            if (type == null)
                return "";
            return TypeSubstitution.Template(Method, type);
        }

        public string Map(ITypeSymbol? type) {
            if (type == null)
                return "";
            return TypeSubstitution.Map(Method, type);
        }
    }
}
