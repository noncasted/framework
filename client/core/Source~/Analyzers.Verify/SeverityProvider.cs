using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Framework.Analyzers.Verify
{
    // dotnet_diagnostic.<id>.severity of the .editorconfig of a case, per file, as the compiler takes it in a build.
    public sealed class SeverityProvider : SyntaxTreeOptionsProvider
    {
        public SeverityProvider(Dictionary<string, ImmutableDictionary<string, ReportDiagnostic>> severities)
        {
            _severities = severities;
        }

        private readonly Dictionary<string, ImmutableDictionary<string, ReportDiagnostic>> _severities;

        public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken)
        {
            return GeneratedKind.Unknown;
        }

        public override bool TryGetDiagnosticValue(SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken,
            out ReportDiagnostic severity)
        {
            return _severities[tree.FilePath].TryGetValue(diagnosticId, out severity);
        }

        public override bool TryGetGlobalDiagnosticValue(string diagnosticId, CancellationToken cancellationToken,
            out ReportDiagnostic severity)
        {
            severity = ReportDiagnostic.Default;
            return false;
        }
    }
}
