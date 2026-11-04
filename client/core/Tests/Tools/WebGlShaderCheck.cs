using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Internal
{
    // The shaders of a game compiled for WebGL (GLES3x, the compiler of the player build), searched for what is slow or
    // hangs on the GPUs of iOS (WEBGL_TROUBLESHOOTING.md of the framework):
    // - G2, an array indexed at run time: HLSLcc writes a table of constants (a const or static const array, a switch fxc
    //   turns into a lookup) as a non-const "vec4 ImmCB_N[...]" and a local array of values as "TempArrayN[...]", both
    //   filled by an assignment per element at the top of main() of every pixel;
    // - G1, a flat varying (nointerpolation): ANGLE on Metal rewrites the index buffer of every draw of such a program.
    // Every pass, its variant without keywords and with each of its keywords alone (the XR ones aside: WebGL has no
    // stereo). For GLES the vertex program comes with the fragment one in one text, so one compile covers both.
    public static class WebGlShaderCheck
    {
        private static readonly (Regex Pattern, string Problem)[] Problems =
        {
            (new Regex(@"\bImmCB_"), "ImmCB_ (a table of constants indexed at run time, G2)"),
            (new Regex(@"\bTempArray"), "TempArray (a local array indexed at run time, G2)"),
            (new Regex(@"\bflat\s+(?:in|out)\b"), "flat varying (nointerpolation, G1)")
        };

        // The .shader assets under the folders ("Assets", "Packages/<name>"), without the paths that start with one of
        // the excluded prefixes (third-party shaders that are not the game's to change).
        public static IReadOnlyList<Shader> Find(IReadOnlyList<string> folders, IReadOnlyList<string> excluded)
        {
            return AssetDatabase.FindAssets("t:Shader", folders.ToArray())
                                .Select(AssetDatabase.GUIDToAssetPath)
                                .Where(path => path.EndsWith(".shader"))
                                .Where(path => excluded.Any(path.StartsWith) == false)
                                .Select(AssetDatabase.LoadAssetAtPath<Shader>)
                                .ToArray();
        }

        // Fails with every problem found (shader, subshader, pass, keywords: what) and every variant that does not compile
        // for WebGL.
        public static void AssertSafe(IReadOnlyList<Shader> shaders)
        {
            Assert.IsNotEmpty(shaders, "no shader to check");

            var report = Check(shaders);

            Assert.GreaterOrEqual(report.Compiled, shaders.Count, "every shader has at least one variant compiled");
            Assert.IsEmpty(report.Problems, "the WebGL code of the shaders has\n" + string.Join("\n", report.Problems));
        }

        public static WebGlShaderReport Check(IReadOnlyList<Shader> shaders)
        {
            var problems = new List<string>();
            var compiled = 0;

            foreach (var shader in shaders)
            {
                var data = ShaderUtil.GetShaderData(shader);

                for (var subshader = 0; subshader < data.SubshaderCount; subshader++)
                {
                    var passes = data.GetSubshader(subshader);

                    for (var pass = 0; pass < passes.PassCount; pass++)
                    {
                        foreach (var keywords in KeywordSets(shader, subshader, pass))
                        {
                            var name = $"{shader.name} subshader {subshader} pass {pass} [{string.Join(" ", keywords)}]";
                            compiled++;
                            CheckVariant(passes.GetPass(pass), keywords, name, problems);
                        }
                    }
                }
            }

            return new WebGlShaderReport(compiled, problems);
        }

        private static IEnumerable<string[]> KeywordSets(Shader shader, int subshader, int pass)
        {
            var keywords = ShaderUtil.GetPassKeywords(shader, new PassIdentifier((uint)subshader, (uint)pass));

            return new[] { new string[0] }
                .Concat(keywords.Where(keyword => keyword.name.Contains("STEREO") == false)
                                .Select(keyword => new[] { keyword.name }));
        }

        private static void CheckVariant(ShaderData.Pass pass, string[] keywords, string name, List<string> problems)
        {
            var result = pass.CompileVariant(ShaderType.Vertex, keywords, ShaderCompilerPlatform.GLES3x, BuildTarget.WebGL);

            if (result.Success == false)
            {
                var messages = string.Join("; ", result.Messages.Select(message => message.message));
                problems.Add($"{name}: does not compile for WebGL: {messages}");
                return;
            }

            var source = Encoding.ASCII.GetString(result.ShaderData);

            if (source.Contains("#ifdef FRAGMENT") == false)
            {
                problems.Add($"{name}: no fragment program in the WebGL code");
                return;
            }

            problems.AddRange(Problems.Where(problem => problem.Pattern.IsMatch(source))
                                      .Select(problem => $"{name}: {problem.Problem}"));
        }
    }
}
