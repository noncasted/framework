using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class WalkState {
        public IMethodSymbol Method;
        public SemanticModel Model;
        public GraphMethod Result;
        public Dictionary<string, WalkLocal> Locals = new Dictionary<string, WalkLocal>(StringComparer.Ordinal);
        public HashSet<string> TaintedBuilder = new HashSet<string>(StringComparer.Ordinal);
        public Dictionary<ITypeParameterSymbol, ITypeSymbol> Substitution = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        public Dictionary<string, bool> Bools = new Dictionary<string, bool>(StringComparer.Ordinal);
        public GraphRegistration? LastRegistration;
        public InvocationExpressionSyntax? LastInstantiate;
        public int LastCallIndex = -1;
        public List<string> Branches = new List<string>();
        public int NextBranch;
        public int NextOrdinal;
        public bool EntityRoot;

        public WalkState(IMethodSymbol method, SemanticModel model, GraphMethod result) {
            Method = method;
            Model = model;
            Result = result;
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
