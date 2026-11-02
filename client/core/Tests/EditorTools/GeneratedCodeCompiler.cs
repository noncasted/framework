using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace Internal.EditorTests
{
    // Generated code is checked by compiling it, not by looking for names: the C# rules on member names (CS0102,
    // CS0542, CS0108, keywords) are the compiler's. The editor ships the Roslyn compiler of its .NET SDK; the
    // references are the ones Unity gives the content assembly, so the code sees what it sees in a project. It runs on
    // the compiler server the editor keeps anyway (-shared): the same compiler without starting a runtime per compile,
    // 0.08 s against 0.25 s and more under load. Without a server it starts one or compiles in its own process.
    public static class GeneratedCodeCompiler
    {
        // Hiding an inherited member is a warning, but in the generated code it is a bug.
        private const string WarningsAsErrors = "CS0108,CS0114";

        public static List<string> Compile(params string[] sources)
        {
            var directory = Path.GetFullPath(Path.Combine("Temp", "CodegenTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);

            try
            {
                var arguments = new List<string>
                {
                    Quote(FindCompiler()),
                    "-nologo",
                    "-noconfig",
                    "-nostdlib+",
                    "-t:library",
                    "-langversion:9",
                    "-warnaserror+:" + WarningsAsErrors,
                    "-out:" + Quote(Path.Combine(directory, "Generated.dll"))
                };

                foreach (var reference in References())
                    arguments.Add("-r:" + Quote(reference));

                for (var index = 0; index < sources.Length; index++)
                {
                    var path = Path.Combine(directory, $"Source{index}.cs");
                    File.WriteAllText(path, sources[index]);
                    arguments.Add(Quote(path));
                }

                var responseFile = Path.Combine(directory, "csc.rsp");
                File.WriteAllLines(responseFile, arguments.Skip(1));

                var start = new ProcessStartInfo(FindDotnet(), arguments[0] + " -shared @" + Quote(responseFile))
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(start);
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();

                var errors = output
                    .Split('\n')
                    .Select(line => line.Trim())
                    .Where(line => line.Contains(": error "))
                    .ToList();

                if (process.ExitCode != 0 && errors.Count == 0)
                    throw new InvalidOperationException("The compiler failed without errors: " + output);

                return errors;
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static IEnumerable<string> References()
        {
            var content = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .Single(assembly => assembly.name == FrameworkSettings.ContentAssembly);

            return content.allReferences
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.Ordinal);
        }

        private static string SdkRoot => Path.Combine(EditorApplication.applicationContentsPath, "DotNetSdk");

        private static string FindDotnet()
        {
            var path = Path.Combine(SdkRoot, "dotnet");
            return File.Exists(path) ? path : path + ".exe";
        }

        private static string FindCompiler()
        {
            return Directory.GetDirectories(Path.Combine(SdkRoot, "sdk"))
                .Select(sdk => Path.Combine(sdk, "Roslyn", "bincore", "csc.dll"))
                .First(File.Exists);
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }
    }
}
