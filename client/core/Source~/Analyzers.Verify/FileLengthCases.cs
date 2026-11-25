using System.Text;

namespace Framework.Analyzers.Verify
{
    // FWR002: the line limit of runtime and editor code; generated code, tests and benchmarks have none.
    internal static class FileLengthCases
    {
        public static void Run(Harness harness)
        {
            harness.Case("FWR002 301 lines", One("Runtime/A.cs", Lines(301, false)), new[] { "FWR002@1" });
            harness.Case("FWR002 300 lines", One("Runtime/A.cs", Lines(300, false)), new string[0]);
            harness.Case("FWR002 300 lines and a final line break", One("Runtime/A.cs", Lines(300, true)), new string[0]);
            harness.Case("FWR002 301 lines of an editor tool", One("Editor/Tools/A.cs", Lines(301, true)), new[] { "FWR002@1" });

            harness.Case("FWR002 test folders", new[]
            {
                new Source("Tests/A.cs", Lines(400, true)),
                new Source("Common/Tests~/B.cs", Lines(400, true, "B")),
                new Source("Game.Tests/C.cs", Lines(400, true, "C")),
                new Source("Tools/Benchmarks/D.cs", Lines(400, true, "D"))
            }, new string[0]);

            harness.Case("FWR002 test assemblies", One("Runtime/A.cs", Lines(400, true)), new string[0], assemblyName: "Game.PlayMode.Tests");
            harness.Case("FWR002 benchmark assembly", One("Runtime/A.cs", Lines(400, true)), new string[0], assemblyName: "Benchmarks");
            harness.Case("FWR002 test tools assembly", One("Runtime/A.cs", Lines(400, true)), new string[0], assemblyName: "Internal.TestTools");
            harness.Case("FWR002 a runtime folder named Benchmark is runtime", One("Console/Benchmark/A.cs", Lines(400, true)),
                new[] { "FWR002@1" });
            harness.Case("FWR002 generated code", One("Runtime/A.g.cs", Lines(400, true)), new string[0]);

            harness.Case("FWR002 framework_rules.max_file_lines", One("Runtime/A.cs", Lines(11, true)), new[] { "FWR002@1" },
                editorConfig: "framework_rules.max_file_lines = 10\n");
            harness.Case("FWR002 framework_rules.code_kind = test", One("Runtime/A.cs", Lines(400, true)), new string[0],
                editorConfig: "framework_rules.code_kind = test\n");
            harness.Case("FWR002 framework_rules.code_kind = runtime in a test folder", One("Tests/A.cs", Lines(400, true)),
                new[] { "FWR002@1" }, editorConfig: "framework_rules.code_kind = runtime\n");

            harness.Case("FWR002 severity none in .editorconfig", One("Runtime/A.cs", Lines(400, true)), new string[0],
                editorConfig: "dotnet_diagnostic.FWR002.severity = none\n");
            harness.Case("FWR002 severity none for one folder", new[]
            {
                new Source("Runtime/A.cs", Lines(400, true)),
                new Source("Legacy/B.cs", Lines(400, true, "B"))
            }, new[] { "FWR002@1" }, editorConfig: "[Legacy/**.cs]\ndotnet_diagnostic.FWR002.severity = none\n");
        }

        private static Source[] One(string path, string text)
        {
            return new[] { new Source(path, text) };
        }

        // A file of the given number of lines: a type and comment lines.
        private static string Lines(int count, bool finalBreak, string type = "A")
        {
            var builder = new StringBuilder();
            builder.Append("public class ").Append(type).Append("\n{\n");

            for (var i = 0; i < count - 3; i++)
                builder.Append("    // line\n");

            builder.Append('}');

            if (finalBreak)
                builder.Append('\n');

            return builder.ToString();
        }
    }
}
