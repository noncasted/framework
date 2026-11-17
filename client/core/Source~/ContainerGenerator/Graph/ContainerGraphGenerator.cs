using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ContainerGenerator {
    [Generator]
    public sealed class ContainerGraphGenerator : IIncrementalGenerator {
        // A name or a dotted prefix of it: a game assembly may well start with one of these words (Systems.Battle).
        private static readonly string[] SkippedAssemblies = {
            "Unity", "UnityEngine", "UnityEditor", "System", "Microsoft", "VContainer", "mscorlib", "netstandard", "ContainerGenerator",
        };

        public void Initialize(IncrementalGeneratorInitializationContext context) {
            context.RegisterSourceOutput(context.CompilationProvider, static (production, compilation) => {
                if (ShouldSkip(compilation.AssemblyName))
                    return;

                var references = ReferenceSymbols.Create(compilation);
                if (references == null || references.ScopeBuilder == null && references.EntityBuilder == null && references.Builder == null)
                    return;

                GraphDocument document;
                try {
                    var harvest = ManifestHarvest.TryBuild(compilation, references);
                    var walkCompilation = compilation;
                    var walkReferences = references;
                    if (harvest != null && string.IsNullOrEmpty(harvest.Source) == false) {
                        walkCompilation = compilation.AddSyntaxTrees(
                            CSharpSyntaxTree.ParseText(harvest.Source, ParseOptions(compilation), path: ManifestHarvest.Path));
                        walkReferences = ReferenceSymbols.Create(walkCompilation) ?? references;
                    }

                    document = new GraphWalker(walkCompilation, walkReferences).Walk();
                    ManifestHarvest.Remap(document, harvest);
                    ManifestReader.Merge(document, compilation);
                    ManifestReader.ReportUnresolved(document);
                    var assets = EntityAssetIndex.Read(walkCompilation);
                    for (var i = 0; i < assets.Diagnostics.Count; i++)
                        production.ReportDiagnostic(assets.Diagnostics[i].ToDiagnostic());
                    EntityAssets.Bind(document, assets);
                }
                catch (System.Exception exception) {
                    production.ReportDiagnostic(Diagnostic.Create(
                        GraphDescriptors.UncoveredSyntax,
                        Location.None,
                        "generator exception: " + exception,
                        compilation.AssemblyName ?? "",
                        "",
                        "0"));
                    return;
                }

                IReadOnlyList<ScopeGraph> graphs;
                try {
                    graphs = EdgeResolver.Resolve(document, compilation, references);
                }
                catch (System.Exception exception) {
                    production.ReportDiagnostic(Diagnostic.Create(
                        GraphDescriptors.UncoveredSyntax,
                        Location.None,
                        "generator exception: " + exception,
                        compilation.AssemblyName ?? "",
                        "",
                        "0"));
                    graphs = System.Array.Empty<ScopeGraph>();
                }

                foreach (var diagnostic in document.Diagnostics)
                    production.ReportDiagnostic(diagnostic.ToDiagnostic());

                for (var i = 0; i < graphs.Count; i++) {
                    var graph = graphs[i];
                    for (var d = 0; d < graph.Diagnostics.Count; d++)
                        production.ReportDiagnostic(graph.Diagnostics[d].ToDiagnostic());

                    // An exception escaping the generator drops every source of the assembly with only the CS8785
                    // warning: the scope that failed is reported as an error, the others are still emitted.
                    try {
                        if (ScopeEmitter.TryEmit(graph, document, compilation, references, out var hint, out var source) == false)
                            continue;

                        production.AddSource(hint, source);
                    }
                    catch (System.Exception exception) {
                        production.ReportDiagnostic(Diagnostic.Create(
                            GraphDescriptors.UncoveredSyntax,
                            Location.None,
                            "generator exception while emitting the scope: " + exception,
                            graph.RootId,
                            "",
                            "0"));
                    }
                }

                if (document.Methods.Count == 0)
                    return;

                try {
                    production.AddSource("ContainerGraph.g.cs", GraphEmitter.Emit(document, compilation));
                }
                catch (System.Exception exception) {
                    production.ReportDiagnostic(Diagnostic.Create(
                        GraphDescriptors.UncoveredSyntax,
                        Location.None,
                        "generator exception while emitting the manifest: " + exception,
                        compilation.AssemblyName ?? "",
                        "",
                        "0"));
                }
            });
        }

        private static CSharpParseOptions ParseOptions(Compilation compilation) {
            foreach (var tree in compilation.SyntaxTrees) {
                if (tree.Options is CSharpParseOptions options)
                    return options;
            }

            return CSharpParseOptions.Default;
        }

        private static bool ShouldSkip(string? name) {
            if (string.IsNullOrEmpty(name))
                return true;

            var assemblyName = name ?? "";
            for (var i = 0; i < SkippedAssemblies.Length; i++) {
                var skipped = SkippedAssemblies[i];
                if (assemblyName == skipped || assemblyName.StartsWith(skipped + ".", System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
