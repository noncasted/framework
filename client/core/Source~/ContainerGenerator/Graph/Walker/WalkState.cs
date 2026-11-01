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
    }
}
