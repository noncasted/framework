using System.Collections.Generic;

namespace Internal
{
    // What WebGlShaderCheck found: the number of variants compiled and one line per problem.
    public readonly struct WebGlShaderReport
    {
        public WebGlShaderReport(int compiled, IReadOnlyList<string> problems)
        {
            Compiled = compiled;
            Problems = problems;
        }

        public int Compiled { get; }
        public IReadOnlyList<string> Problems { get; }
    }
}
