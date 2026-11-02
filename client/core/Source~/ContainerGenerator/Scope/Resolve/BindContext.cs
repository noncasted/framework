using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    // What the binding of the registrations of one scope looks at.
    internal sealed class BindContext {
        public BindContext(
            ScopeGraph graph,
            TypeIndex types,
            Compilation compilation,
            ReferenceSymbols references,
            HashSet<string> implicitTypes,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType,
            Dictionary<string, string> parentExports) {
            Graph = graph;
            Types = types;
            Compilation = compilation;
            References = references;
            ImplicitTypes = implicitTypes;
            LastByType = lastByType;
            AllByType = allByType;
            ParentExports = parentExports;
        }

        public ScopeGraph Graph { get; }
        public TypeIndex Types { get; }
        public Compilation Compilation { get; }
        public ReferenceSymbols References { get; }
        public HashSet<string> ImplicitTypes { get; }
        public Dictionary<string, int> LastByType { get; }
        public Dictionary<string, List<int>> AllByType { get; }
        public Dictionary<string, string> ParentExports { get; }
    }
}
