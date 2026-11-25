using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Framework.Analyzers.Verify
{
    // The analyzer options of the .editorconfig of a case (framework_rules.*), per file.
    public sealed class OptionsProvider : AnalyzerConfigOptionsProvider
    {
        public OptionsProvider(Dictionary<SyntaxTree, ImmutableDictionary<string, string>> options)
        {
            _options = options;
        }

        private static readonly AnalyzerConfigOptions _empty = new Options(ImmutableDictionary<string, string>.Empty);

        private readonly Dictionary<SyntaxTree, ImmutableDictionary<string, string>> _options;

        public override AnalyzerConfigOptions GlobalOptions => _empty;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return new Options(_options[tree]);
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return _empty;
        }

        private sealed class Options : AnalyzerConfigOptions
        {
            public Options(ImmutableDictionary<string, string> values)
            {
                _values = values;
            }

            private readonly ImmutableDictionary<string, string> _values;

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                return _values.TryGetValue(key, out value);
            }
        }
    }
}
